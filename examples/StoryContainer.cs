using Godot;
using System;

using GodotArray = Godot.Collections.Array;

public partial class StoryContainer : Control
{
    [Signal]
    public delegate void ChoiceClickEventHandler(int choiceIndex);

    private ScrollContainer scroll;
    private BoxContainer container;

    public override void _Ready()
    {
        scroll = GetNode<ScrollContainer>("VMargin/Scroll");
        container = GetNode<BoxContainer>("VMargin/Scroll/HMargin/StoryContainer");
    }

    public async void Add(CanvasItem newNode, float delay = 0)
    {
        container.AddChild(newNode);
        await ToSignal(GetTree(), "process_frame");
        scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
    }

    public void CleanChoices()
    {
        // Remove all nodes in choiceButtons group
        foreach (Node choice in GetTree().GetNodesInGroup("choiceButtons"))
            choice.QueueFree();
    }

    public Label CreateText(String text)
    {
        Label label = new Label()
        {
            AutowrapMode = TextServer.AutowrapMode.Word,
            Text = text,
        };
        return label;
    }

    public Button CreateChoice(String text, int choiceIndex)
    {
        Button button = new Button()
        {
            Text = text,
            SizeFlagsHorizontal = (int)SizeFlags.ShrinkCenter,
        };
        button.AddToGroup("choiceButtons");
        button.Pressed += () =>
        {
            EmitSignal(nameof(ChoiceClick), choiceIndex);
        };
        return button;
    }

    public HSeparator CreateSeparation()
    {
        return new HSeparator();
    }
}
