#!/usr/bin/env python3
"""Make a small SIL-OFL interface font from an authorized Noto Sans CJK source.
Use a separate Python venv with fonttools; pass the original downloaded OTF path.
"""
from pathlib import Path
import sys,hashlib,json
from fontTools import subset
from fontTools.ttLib import TTFont
root=Path(__file__).resolve().parents[1];source=Path(sys.argv[1])
chars=set(chr(i) for i in range(32,127))
for f in (root/'Assets').rglob('*.cs'):
    chars.update(c for c in f.read_text() if ord(c)>=127 and not c.isspace())
font=TTFont(source);available=set(font.getBestCmap());wanted={ord(c) for c in chars};missing=wanted-available
if missing: print('Source does not cover non-UI source symbols:', ''.join(chr(c) for c in sorted(missing)))
wanted &= available
options=subset.Options();options.name_IDs=['*'];options.name_legacy=True;options.name_languages=['*']
sub=subset.Subsetter(options=options);sub.populate(unicodes=wanted);sub.subset(font)
# New primary family name for a modified subset; original copyright/license records retained.
for item in font['name'].names:
    names={1:'AeroPulse SC',2:'Regular',3:'AeroPulseSC-Subset-1.0',4:'AeroPulse SC Regular',6:'AeroPulseSC-Regular',16:'AeroPulse SC',17:'Regular'}
    if item.nameID in names:item.string=names[item.nameID].encode(item.getEncoding())
output=root/'Assets/Fonts/Source/AeroPulseSC-Regular.otf';output.parent.mkdir(parents=True,exist_ok=True);font.save(output)
(root/'Assets/Fonts/InterfaceCharacters.txt').write_text(''.join(chr(c) for c in sorted(wanted)))
(root/'Assets/Fonts/SourceInfo.json').write_text(json.dumps({'upstream':'https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf','source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'license':'SIL Open Font License1.1; see OFL-NotoSansCJK.txt','modified_family':'AeroPulse SC','glyphs_requested':len(wanted),'subset_bytes':output.stat().st_size},indent=2))
print('subset:',len(wanted),'characters;',output.stat().st_size,'bytes')
