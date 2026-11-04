# Product media

`scripts/social-preview.ps1` regenerates the approved 2400 × 1260 social card from the actual sample picker in `site/assets/app.png`. Its cache-versioned URL is declared in the site's Open Graph and Twitter metadata. `scripts/branding.ps1` also calls this renderer.

The SourceForge gallery uses the original 1400 × 1520 `app.png` and `settings.png` files, with separate captions. Its eight feature bullets describe the current app and all three download choices. Keep the Windows default on the latest self-contained installer after an automatic release import.

Run `scripts/demo-video.ps1` on Windows to export isolated WPF sample views and render a silent, captioned 1920 × 1080 demonstration. Requirements: .NET SDK, Python, Pillow and imageio-ffmpeg. Output is under `artifacts/video`, including the MP4 and SRT captions. No real window titles, production preferences or recording of the user's desktop are used. Review the generated video before publishing.

The approved video is public at https://www.youtube.com/watch?v=2ecOkoOEgUc and linked from the README, product site and SourceForge video field. The site also serves the exact approved MP4 with English captions and a local poster; playback loads only on request. Its duration is 40.9 seconds. The user selected Holden's preset voice, generated with Higgsfield Seed Audio (job `4f7a99c3-5e6f-4a7f-9bd8-be4e74dbf98e`). Readable addresses and shortcut labels were corrected without changing encoded audio bytes or scene timing.

The rejected Windows-stock-voice draft remains private in YouTube Studio as `2rEuiQlSj-E`; do not publish or link it.

To render approved narration locally, pass `--audio <wav>` and `--durations <json>` to `scripts/render-demo.py`. The JSON contains seven positive scene durations; align their boundaries with the narration's paragraph pauses before rendering. Both silent and narrated output retain on-screen copy and produce SRT captions. Keep audio generation separate so rebuilding the video does not spend generation credits.
