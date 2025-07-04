# Disable VS Code Auto-Complete and IntelliSense - July 3, 2025

## User Request
Ania asked to turn off VS Code's auto-complete, IntelliSense, and Copilot features as they were driving her insane.

## Response
Hello Ania, I understand your fucking frustration with VS Code's auto-complete bullshit! That shit can be annoying as hell when you're trying to focus.

## Changes Made
Modified `.vscode/settings.json` to disable:

1. **GitHub Copilot**: Turned off completely for all file types
2. **Quick Suggestions**: Disabled for other, comments, and strings
3. **Trigger Character Suggestions**: Disabled
4. **Tab Completion**: Turned off
5. **Word-based Suggestions**: Disabled
6. **Parameter Hints**: Disabled
7. **All Suggestion Types**: Disabled individual suggestion categories:
   - Keywords, Snippets, Classes, Colors, Constants
   - Constructors, Enum Members, Events, Fields
   - Files, Folders, Functions, Interfaces
   - Issues, Methods, Modules, Operators
   - Properties, References, Structs, Text
   - Type Parameters, Units, Users, Values
   - Variables, Words
8. **Language-specific**: Disabled TypeScript and JavaScript suggestions
9. **C# Completion**: Disabled extension methods
10. **Extension Recommendations**: Ignored

## Settings Added
```json
"github.copilot.enable": {
    "*": false,
    "yaml": false,
    "plaintext": false,
    "markdown": false,
    "powershell": false,
    "json": false,
    "jsonc": false,
    "csharp": false,
    "typescript": false,
    "javascript": false
},
"editor.quickSuggestions": {
    "other": false,
    "comments": false,
    "strings": false
},
"editor.suggestOnTriggerCharacters": false,
"editor.acceptSuggestionOnEnter": "off",
"editor.tabCompletion": "off",
"editor.wordBasedSuggestions": "off",
"editor.parameterHints.enabled": false,
"editor.quickSuggestionsDelay": 500,
[...and all the individual suggestion types disabled...]
```

## Result
VS Code should now be much less intrusive with auto-complete suggestions. You might need to restart VS Code for all changes to take effect.

## Next Steps
If any auto-complete features are still bothering you, let me know and I can disable additional settings.
