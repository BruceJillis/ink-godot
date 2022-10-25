@tool
extends EditorPlugin

var import_plugin

var settings = {
	"ink/inklecate_path": {
		"type": TYPE_STRING,
		"hint": PROPERTY_HINT_FILE, 
		"hint_string": OS.get_name() == "OSX" if "" else "*.exe",
		"default": ""
	},
	"ink/marshall_state_variables": {
		"type": TYPE_BOOL,
		"hint_string": "Enable this if you're going to use state variables from GDScript.",
		"default": false
	}
}

func _enter_tree():
	for key in settings.keys():
		if !ProjectSettings.has_setting(key):
			ProjectSettings.set_setting(key, settings[key]['default'])
		ProjectSettings.add_property_info({
			"name": key,
			"type": settings[key]['type'],
			"hint": settings[key].get("hint", null),
			"hint_string": settings[key].get("hint_string", null)
		})
		ProjectSettings.set_initial_value(key, settings[key]['default'])
	ProjectSettings.save()

	add_custom_type("InkPlayer", "Node", preload("InkPlayer.cs"), preload("icon.svg"))

	import_plugin = preload("InkImporter.gd").new()
	add_import_plugin(import_plugin);


func _exit_tree():
	remove_import_plugin(import_plugin)
	import_plugin = null
		
	remove_custom_type("InkPlayer")
	
	for key in settings.keys():
		if ProjectSettings.has_setting(key):
			ProjectSettings.set_setting(key, null)

