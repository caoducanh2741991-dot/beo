"""Build a legible, code-native multi-resolution Windows icon; no font dependencies."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
ROOT=Path(__file__).resolve().parent/'assets';ROOT.mkdir(exist_ok=True)
S=1024
im=Image.new('RGBA',(S,S));mask=Image.new('L',(S,S));d=ImageDraw.Draw(mask);d.rounded_rectangle((28,28,996,996),radius=228,fill=255)
pix=im.load()
for y in range(S):
 for x in range(S):
  t=(x+y)/(2*S);pix[x,y]=(int(16+5*t),int(31+27*t),int(57+27*t),255)
im.putalpha(mask)
glow=Image.new('RGBA',(S,S));g=ImageDraw.Draw(glow);g.ellipse((300,400,1200,1250),fill=(0,190,169,110));glow=glow.filter(ImageFilter.GaussianBlur(140));im=Image.alpha_composite(im,glow);im.putalpha(mask)
d=ImageDraw.Draw(im)
# Six bars form a clean speech waveform with rounded caps.
for x,top,bottom,color in [(202,432,592,'#6ee7d1'),(326,307,717,'#a2f0df'),(450,214,810,'#ffffff'),(574,338,686,'#ffffff'),(698,280,744,'#a2f0df'),(822,419,605,'#6ee7d1')]:
 d.rounded_rectangle((x-38,top,x+38,bottom),radius=38,fill=color)
im.resize((256,256),Image.Resampling.LANCZOS).save(ROOT/'V15VoiceStudio.png')
import struct
sizes=[16,24,32,48,64,128,256];images=[];directory=[];offset=6+16*len(sizes)
for size in sizes:
 small=im.resize((size,size),Image.Resampling.LANCZOS)
 bgra=small.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes('raw','BGRA')
 stride=((size+31)//32)*4;maskdata=bytearray(stride*size)
 for y in range(size):
  for x in range(size):
   if small.getpixel((x,size-y-1))[3]==0:maskdata[y*stride+x//8]|=128>>(x%8)
 data=struct.pack('<IiiHHIIiiII',40,size,size*2,1,32,0,len(bgra)+len(maskdata),0,0,0,0)+bgra+maskdata
 directory.append(struct.pack('<BBBBHHII',size%256,size%256,0,0,1,32,len(data),offset));images.append(data);offset+=len(data)
(ROOT/'V15VoiceStudio.ico').write_bytes(struct.pack('<HHH',0,1,len(sizes))+b''.join(directory)+b''.join(images))

svg='''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024"><defs><linearGradient id="b" x2="1" y2="1"><stop stop-color="#101f39"/><stop offset="1" stop-color="#154655"/></linearGradient></defs><rect x="28" y="28" width="968" height="968" rx="228" fill="url(#b)"/>'''
for x,top,bottom,color in [(202,432,592,'#6ee7d1'),(326,307,717,'#a2f0df'),(450,214,810,'#ffffff'),(574,338,686,'#ffffff'),(698,280,744,'#a2f0df'),(822,419,605,'#6ee7d1')]:svg+=f'<rect x="{x-38}" y="{top}" width="76" height="{bottom-top}" rx="38" fill="{color}"/>'
(ROOT/'V15VoiceStudio.svg').write_text(svg+'</svg>')
print('ICO: seven sizes 16–256px, transparent rounded corners')
