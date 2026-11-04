from pathlib import Path
import argparse, json, textwrap, subprocess
from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg
root=Path(__file__).resolve().parents[1]
out=root/'artifacts'/'video'
parser=argparse.ArgumentParser()
parser.add_argument('--audio',type=Path)
parser.add_argument('--durations',type=Path,help='JSON array of seven scene durations in seconds')
options=parser.parse_args()
scenes=json.loads((out/'scenes.json').read_text(encoding='utf-8-sig'))
durations=json.loads(options.durations.read_text()) if options.durations else None
if options.audio and not durations:
    parser.error('Narrated rendering requires reviewed scene durations.')
if durations and (len(durations)!=len(scenes) or any(not isinstance(d,(int,float)) or d<=0 for d in durations)):
    parser.error('Each scene needs a positive duration.')
fonts=Path('C:/Windows/Fonts')
regular=lambda n: ImageFont.truetype(str(fonts/'segoeui.ttf'),n)
bold=lambda n: ImageFont.truetype(str(fonts/'segoeuib.ttf'),n)
icon=Image.open(root/'site/assets/icon.png').convert('RGBA').resize((74,74),Image.Resampling.LANCZOS)
concat=[]; subtitle=[]; elapsed=0.0
for i,scene in enumerate(scenes):
    duration=durations[i] if durations else max(3.5,min(7.0,len(scene['text'].split())/2.8+1))
    canvas=Image.new('RGBA',(1920,1080),'#F5F7FC'); draw=ImageDraw.Draw(canvas)
    draw.rounded_rectangle((995,-100,2030,1200),radius=110,fill='#E6ECFF')
    canvas.alpha_composite(icon,(70,65))
    draw.text((166,81),'VOLTURA',font=bold(30),fill='#3656E8')
    draw.text((72,180),'HARDTOP FOR WINDOWS',font=bold(22),fill='#5D6A83')
    draw.multiline_text((66,270),scene['title'],font=bold(67),fill='#18213A',spacing=14)
    wrapped='\n'.join(textwrap.fill(line,width=44) for line in scene['text'].splitlines())
    draw.multiline_text((72,530),wrapped,font=regular(31),fill='#5D6A83',spacing=12)
    draw.text((72,914),'hardtop.voltura.se',font=bold(29),fill='#3656E8')
    draw.text((72,989),f'{i+1:02d} / 07   ·   Demo with sample windows',font=regular(21),fill='#5D6A83')
    screen=Image.open(out/scene['image']).convert('RGBA')
    screen.thumbnail((800,900),Image.Resampling.LANCZOS)
    x=1050+(800-screen.width)//2; y=(1080-screen.height)//2
    shadow=Image.new('RGBA',canvas.size,(0,0,0,0)); sd=ImageDraw.Draw(shadow)
    sd.rounded_rectangle((x+9,y+14,x+screen.width+9,y+screen.height+14),radius=20,fill=(24,33,58,30))
    canvas=Image.alpha_composite(canvas,shadow); canvas.alpha_composite(screen,(x,y))
    path=out/f'scene-{i:02d}.png';canvas.convert('RGB').save(path,optimize=True)
    concat += [f"file '{path.as_posix()}'",f'duration {duration:.6f}']
    def stamp(t):
        ms=round(t*1000);return f'{ms//3600000:02d}:{ms//60000%60:02d}:{ms//1000%60:02d},{ms%1000:03d}'
    subtitle += [str(i+1),f'{stamp(elapsed+.35)} --> {stamp(elapsed+duration-.5)}',textwrap.fill(scene['text'],width=60),'']
    elapsed+=duration
concat += [f"file '{(out/f'scene-{len(scenes)-1:02d}.png').as_posix()}'"]
(out/'frames.txt').write_text('\n'.join(concat)+'\n',encoding='utf-8')
(out/'hardtop-demo.srt').write_text('\n'.join(subtitle),encoding='utf-8')
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
video=out/('hardtop-demo-narrated.mp4' if options.audio else 'hardtop-demo-silent.mp4')
args=[ffmpeg,'-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',str(out/'frames.txt')]
if options.audio: args+=['-i',str(options.audio.resolve())]
args+=['-vf','fps=30,format=yuv420p','-c:v','libx264','-threads','4','-preset','medium','-crf','18']
args+=['-c:a','aac','-b:a','128k'] if options.audio else ['-an']
args+=['-movflags','+faststart','-shortest',str(video)]
subprocess.run(args,check=True)
print(json.dumps({'file':str(video),'seconds':elapsed,'bytes':video.stat().st_size,'resolution':'1920x1080','audio':str(options.audio) if options.audio else 'none; on-screen captions','app_views':'actual WPF controls with sample data'},indent=2))
