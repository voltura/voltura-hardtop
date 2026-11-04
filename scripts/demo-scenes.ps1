$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$video=Join-Path $root 'artifacts/video'
$scenes=@(
    @{image='01-picker.png';title="Keep a window.`nKeep your focus.";text='Meet Voltura HardTop. Pin the windows you need above the rest.'},
    @{image='02-search.png';title='Find it fast.';text='Search open windows by title or application.'},
    @{image='03-pinned-result.png';title='Pin what matters.';text='Choose Pin to keep a window above the rest.'},
    @{image='04-pinned-view.png';title="Everything pinned,`nin one place.";text='Use the Pinned view to find your pinned windows.'},
    @{image='05-preferences.png';title='Make it yours.';text='Choose from 21 languages, your preferred theme, and adjustable app opacity.'},
    @{image='06-shortcuts.png';title="Your shortcuts.`nYour workflow.";text="Ctrl + Alt + Z opens the picker.`nCtrl + Alt + T toggles the active window."},
    @{image='07-light.png';title="Free for Windows.`nOpen source.";text='Download Voltura HardTop at hardtop.voltura.se.'}
)
$scenes|ConvertTo-Json|Set-Content (Join-Path $video 'scenes.json') -Encoding utf8
Write-Host 'Seven captioned demo scenes prepared.'
