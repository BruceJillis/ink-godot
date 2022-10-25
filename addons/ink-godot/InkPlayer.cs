using Godot;
using Ink.Runtime;
using System;
using System.Collections.Generic;

#if TOOLS
[Tool]
#endif
public partial class InkPlayer : Node
{
    private bool shouldMarshallVariables = false;

    [Export]
    private Resource story = null;

    [Export]
    private bool autoLoadStory = false;

    [Signal]
    public delegate void InkContinuedEventHandler(string text, string[] tags);

    [Signal]
    public delegate void InkChoicesEventHandler(string[] choices);

    [Signal]
    public delegate void InkEndedEventHandler();

    [Signal]
    public delegate void InkErrorEventHandler(string message, bool isWarning);

    public delegate void InkVariableChanged(string variableName, object variableValue);

    private string ObservedVariableSignalName(string name)
    {
        return $"{nameof(InkVariableChanged)}-{name}";
    }

    private readonly List<string> observedVariables = new List<string>();

    private Ink.Runtime.Story.VariableObserver observer;

    public bool CanContinue => inkStory?.canContinue ?? false;
    public string CurrentText => inkStory?.currentText ?? default;
    public string[] CurrentTags => inkStory?.currentTags.ToArray() ?? default;
    public bool HasChoices => inkStory?.currentChoices.Count > 0;
    public string[] CurrentChoices => inkStory?.currentChoices.ConvertAll(choice => choice.text).ToArray() ?? default;
    public string[] GlobalTags => inkStory?.globalTags?.ToArray() ?? default;

    private Ink.Runtime.Story inkStory = null;


    public override void _Ready()
    {
        shouldMarshallVariables = ProjectSettings.HasSetting("ink/marshall_state_variables");

        observer = (string varName, object varValue) =>
        {
            if (observedVariables.Contains(varName))
                EmitSignal(ObservedVariableSignalName(varName), new Variant[] { varName, (Variant)MarshallVariableValue(varValue) });
        };


        if (!autoLoadStory)
            return;

        LoadStory();
    }

    public Error LoadStory()
    {
        Reset();

        if (!IsJSONFileValid())
        {
            GD.PrintErr("The story you're trying to load is not valid.");
            return Error.InvalidData;
        }

        inkStory = new Ink.Runtime.Story(story.GetMeta("content").AsString());
        inkStory.onError += OnStoryError;

        return Error.Ok;
    }

    public Error LoadStory(Resource story)
    {
        this.story = story;
        return LoadStory();
    }

    public Error LoadStory(string story)
    {
        return LoadStory(StoryFromRaw(story));
    }


    public Error LoadStoryAndSetState(string state)
    {
        if (LoadStory() is Error error && error != Error.Ok)
            return error;

        SetState(state);
        return Error.Ok;
    }

    public Error LoadStoryAndSetState(Resource story, string state)
    {
        this.story = story;
        return LoadStoryAndSetState(state);
    }

    public Error LoadStoryAndSetState(string story, string state)
    {
        this.story = StoryFromRaw(story);
        return LoadStoryAndSetState(state);
    }

    public string Continue()
    {
        string text = null;

        // Continue if we can
        if (CanContinue)
        {
            inkStory.Continue();
            text = CurrentText;

            EmitSignal(nameof(InkContinued), new Variant[] { Variant.CreateFrom(CurrentText), Variant.CreateFrom(CurrentTags) });
            // Check if we have choices after continuing
            if (HasChoices)
                EmitSignal(nameof(InkChoices), new Variant[] { Variant.CreateFrom(CurrentChoices) });
        }
        // If we can't continue and don't have any choice, we're at the end
        else if (!HasChoices)
            EmitSignal(nameof(InkEnded));

        return text;
    }

    private void Reset()
    {
        if (inkStory == null)
            return;

        inkStory = null;
    }

    public void ChooseChoiceIndex(int index)
    {
        if (index < 0 || index >= inkStory?.currentChoices.Count) return;
        inkStory.ChooseChoiceIndex(index);
    }


    public string ChooseChoiceIndexAndContinue(int index)
    {
        ChooseChoiceIndex(index);
        return Continue();
    }

    public bool ChoosePathString(string pathString)
    {
        if (inkStory != null)
        {
            try
            {
                inkStory.ChoosePathString(pathString);

                return true;
            }
            catch (Exception e)
            {
                GD.PrintErr(e.ToString());
            }
        }

        return false;
    }

    public void SwitchFlow(string flowName)
    {
        inkStory?.SwitchFlow(flowName);
    }

    public void SwitchToDefaultFlow()
    {
        inkStory?.SwitchToDefaultFlow();
    }

    public void RemoveFlow(string flowName)
    {
        inkStory?.RemoveFlow(flowName);
    }

    public object GetVariable(string name)
    {
        return MarshallVariableValue(inkStory?.variablesState[name]);
    }

    public void SetVariable(string name, object value_)
    {
        if (inkStory == null) return;

        inkStory.variablesState[name] = value_;
    }

    public string ObserveVariable(string name)
    {
        if (inkStory == null)
            return null;

        string signalName = ObservedVariableSignalName(name);

        if (!observedVariables.Contains(name))
        {
            if (!HasUserSignal(signalName))
                AddUserSignal(signalName);

            observedVariables.Add(name);
            inkStory.ObserveVariable(name, observer);
        }

        return signalName;
    }

    private void RemoveVariableObserver(string name, bool clear)
    {
        if (inkStory == null) return;
        if (!observedVariables.Contains(name)) return;

        string signalName = ObservedVariableSignalName(name);
        if (HasUserSignal(signalName))
        {
            Godot.Collections.Array connections = (Godot.Collections.Array)GetSignalConnectionList(signalName);
            foreach (Godot.Collections.Dictionary connection in connections)
                //Disconnect(signalName, connection["target"].AsGodotObject(), connection["method"].AsString());
                Disconnect(signalName, new Callable(connection["target"].AsGodotObject(), connection["method"].AsString()));
            // Seems like there's no way to undo `AddUserSignal` so we're just going to unbind everything :/
        }

        inkStory.RemoveVariableObserver(null, name);

        if (!clear) return;

        observedVariables.Remove(name);
    }

    public void RemoveVariableObserver(string name)
    {
        RemoveVariableObserver(name, true);
    }
    public int VisitCountAtPathString(string pathString)
    {
        return inkStory?.state.VisitCountAtPathString(pathString) ?? 0;
    }

    public void BindExternalFunction(string inkFuncName, Node node, string funcName)
    {
        BindExternalFunction(inkFuncName, node, funcName, false);
    }

    public void BindExternalFunction(string inkFuncName, Node node, string funcName, bool lookaheadSafe)
    {
        inkStory?.BindExternalFunctionGeneral(inkFuncName, (object[] foo) => node.Call(funcName, new Variant[] { Variant.CreateFrom((Span<string>)foo) }), lookaheadSafe);
    }

    public void BindExternalFunction(string inkFuncName, Func<object> func, bool lookaheadSafe)
    {
        inkStory?.BindExternalFunction(inkFuncName, func, lookaheadSafe);
    }

    public void BindExternalFunction(string inkFuncName, Func<object> func)
    {
        BindExternalFunction(inkFuncName, func, false);
    }

    public void BindExternalFunction<T>(string inkFuncName, Func<T, object> func, bool lookaheadSafe)
    {
        inkStory?.BindExternalFunction(inkFuncName, func, lookaheadSafe);
    }

    public void BindExternalFunction<T>(string inkFuncName, Func<T, object> func)
    {
        BindExternalFunction(inkFuncName, func, false);
    }

    public void BindExternalFunction<T1, T2>(string inkFuncName, Func<T1, T2, object> func, bool lookaheadSafe)
    {
        inkStory?.BindExternalFunction(inkFuncName, func, lookaheadSafe);
    }

    public void BindExternalFunction<T1, T2>(string inkFuncName, Func<T1, T2, object> func)
    {
        BindExternalFunction(inkFuncName, func, false);
    }

    public void BindExternalFunction<T1, T2, T3>(string inkFuncName, Func<T1, T2, T3, object> func, bool lookaheadSafe)
    {
        inkStory?.BindExternalFunction(inkFuncName, func, lookaheadSafe);
    }

    public void BindExternalFunction<T1, T2, T3>(string inkFuncName, Func<T1, T2, T3, object> func)
    {
        BindExternalFunction(inkFuncName, func, false);
    }

    public void BindExternalFunction<T1, T2, T3, T4>(string inkFuncName, Func<T1, T2, T3, T4, object> func, bool lookaheadSafe)
    {
        inkStory?.BindExternalFunction(inkFuncName, func, lookaheadSafe);
    }

    public void BindExternalFunction<T1, T2, T3, T4>(string inkFuncName, Func<T1, T2, object> func)
    {
        BindExternalFunction(inkFuncName, func, false);
    }

    public object EvaluateFunction(string functionName, bool returnTextOutput, params object[] arguments)
    {
        if (!returnTextOutput)
            return inkStory?.EvaluateFunction(functionName, arguments);

        string textOutput = null;
        object returnValue = inkStory?.EvaluateFunction(functionName, out textOutput, arguments);
        return new object[] { returnValue, textOutput };
    }

    private object MarshallVariableValue(object value)
    {
        if (!shouldMarshallVariables)
            return value;

        if (value != null && value.GetType() == typeof(Ink.Runtime.InkList))
            value = null;

        return value;
    }

    public string GetState()
    {
        return inkStory.state.ToJson();
    }

    public void SetState(string state)
    {
        inkStory.state.LoadJson(state);
    }

    public void SaveStateOnDisk(string path)
    {
        if (!path.StartsWith("res://") && !path.StartsWith("user://"))
            path = $"user://{path}";
        /*        File file = new File();
                file.Open(path, File.ModeFlags.Write);
        */
        var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        SaveStateOnDisk(file);
    }

    public void SaveStateOnDisk(FileAccess file)
    {
        if (!file.IsOpen())
            return;

        file.StoreString(GetState());
    }

    public void LoadStateFromDisk(string path)
    {
        if (!path.StartsWith("res://") && !path.StartsWith("user://"))
            path = $"user://{path}";
        /*        File file = new File();
                file.Open(path, File.ModeFlags.Read);*/
        var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        LoadStateFromDisk(file);
    }

    public void LoadStateFromDisk(FileAccess file)
    {
        if (!file.IsOpen())
            return;

        file.Seek(0);
        if (file.GetLength() > 0)
            inkStory.state.LoadJson(file.GetAsText());
    }

    public string[] TagsForContentAtPath(string pathString)
    {
        return inkStory?.TagsForContentAtPath(pathString)?.ToArray() ?? default;
    }

    private bool IsJSONFileValid() => story?.HasMeta("content") ?? false;


    private void OnStoryError(string message, Ink.ErrorType errorType)
    {
        if (errorType == Ink.ErrorType.Author)
            return;  // This should never happen but eh? What's the cost of checking.

        if (GetSignalConnectionList(nameof(InkError)).Count > 0)
            EmitSignal(nameof(InkError), message, errorType == Ink.ErrorType.Warning);
        else
            GD.PrintErr($"Ink had an error. It is strongly suggested that you connect an error handler to InkError. {message}");
    }



    private Resource StoryFromRaw(string raw)
    {
        var story = new Resource();
        story.SetMeta("content", raw);
        return story;
    }
}
