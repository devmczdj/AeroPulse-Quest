#!/usr/bin/env python3
"""Verify, tag and publish a locally built Quest version; never rebuild or overwrite a tag."""
import argparse, hashlib, json, os, re, shutil, subprocess, tempfile, zipfile
from pathlib import Path

def run(args, **kwargs):
    return subprocess.run(args, check=True, capture_output=True, **kwargs)

def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(1048576),b''):h.update(chunk)
    return h.hexdigest()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('version');parser.add_argument('--title',required=True)
    parser.add_argument('--notes-file',type=Path,required=True)
    parser.add_argument('--evidence-dir',type=Path,required=True)
    parser.add_argument('--source-manifest',type=Path,required=True)
    parser.add_argument('--repo',default='devmczdj/AeroPulse-Quest')
    parser.add_argument('--build-tools',type=Path,required=True)
    args=parser.parse_args();assert re.fullmatch(r'0\.\d+\.\d+',args.version)
    root=Path(__file__).resolve().parents[1];evidence=args.evidence_dir;evidence.mkdir(parents=True,exist_ok=True)
    apk=root/'Builds'/f'AeroPulse-GlassLab-v{args.version}.apk';assert apk.is_file()
    env=os.environ.copy()
    if not env.get('JAVA_HOME'):raise RuntimeError('Set JAVA_HOME to the Unity Android OpenJDK before publishing')
    badging=run([str(args.build_tools/'aapt'),'dump','badging',str(apk)],text=True,env=env).stdout
    assert f"versionName='{args.version}'" in badging and "name='com.three.aeropulsemvp.glass'" in badging
    code=int(re.search(r"versionCode='(\d+)'",badging).group(1))
    signature=run([str(args.build_tools/'apksigner'),'verify','--verbose',str(apk)],text=True,env=env).stdout
    run([str(args.build_tools/'zipalign'),'-c','-P','16','4',str(apk)],text=True)
    with zipfile.ZipFile(apk) as z:assert z.testzip() is None
    sha=digest(apk);sources=json.loads(args.source_manifest.read_text())
    for name,h in sources.items():assert digest(root/name)==h,f'Source changed after build: {name}'
    repo=json.loads(run(['gh','repo','view',args.repo,'--json','visibility,url'],text=True).stdout)
    assert repo['visibility']=='PRIVATE','Release destination must stay private'
    record={'version':args.version,'versionCode':code,'package':'com.three.aeropulsemvp.glass','apk':apk.name,
            'size_bytes':apk.stat().st_size,'sha256':sha,'signature':signature,'zipalign':'PASS','crc':'PASS',
            'source_files_at_build':sources,'scope':'Editor/automated validation; final user headset acceptance pending'}
    docs=root/'Docs/11_Releases';docs.mkdir(exist_ok=True)
    exists=subprocess.run(['git','-C',str(root),'rev-parse','--verify','refs/tags/v'+args.version],capture_output=True).returncode==0
    if not exists:
        previous=[]
        for metadata in docs.glob('0.*.json'):
            item=json.loads(metadata.read_text())
            if item.get('version')!=args.version and 'versionCode' in item:previous.append(item['versionCode'])
        assert code>max(previous,default=0),'Android versionCode must increase'
    if exists:
        old=json.loads(run(['git','-C',str(root),'show',f'v{args.version}:Docs/11_Releases/{args.version}.json'],text=True).stdout)
        assert old['sha256']==sha,'Published tag differs; bump version instead of overwriting'
    else:
        (docs/(args.version+'.json')).write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
        for name in ['Assets','Packages','ProjectSettings','DESIGN.md','README.md','AGENTS.md','CHANGELOG.md','Docs','Tools','.gitignore']:
            if (root/name).exists():run(['git','-C',str(root),'add','--',name],text=True)
        tracked=[p.decode('utf-8') for p in run(['git','-C',str(root),'ls-files','-z']).stdout.split(b'\0') if p]
        assert not any(p.split('/')[0] in ['Library','Temp','Builds','build','Logs','UserSettings'] for p in tracked)
        assert not any(p.lower().endswith(('.apk','.aab','.keystore','.jks','.pem','.key')) for p in tracked)
        credential=re.compile(rb'(gh[pousr]_[A-Za-z0-9]{20,}|sk-[A-Za-z0-9_-]{20,}|AKIA[0-9A-Z]{16}|-----BEGIN (?:RSA |OPENSSH |EC |DSA )?PRIVATE KEY-----)')
        for name in tracked:
            data=(root/name).read_bytes()
            if b'\0' not in data[:8192] and credential.search(data):
                raise RuntimeError('Possible credential in staged file: '+name)

        run(['git','-C',str(root),'diff','--cached','--check','--','*.cs','*.md','*.py','.gitignore'],text=True)
        run(['git','-C',str(root),'commit','--quiet','-m',f'feat: release {args.version} {args.title}'],text=True)
        run(['git','-C',str(root),'tag','-a','v'+args.version,'-m',f'{args.version} code {code}: automated validation; final headset test pending'],text=True)
    commit=run(['git','-C',str(root),'rev-parse',f'v{args.version}^{{commit}}'],text=True).stdout.strip()
    for name,h in sources.items():
        data=run(['git','-C',str(root),'show',f'v{args.version}:{name}']).stdout
        assert hashlib.sha256(data).hexdigest()==h,f'Tag bytes differ: {name}'
    branch=run(['git','-C',str(root),'branch','--show-current'],text=True).stdout.strip()
    if branch!='main':
        run(['git','-C',str(root),'switch','main'],text=True)
        run(['git','-C',str(root),'merge','--ff-only',branch],text=True)
    run(['git','-C',str(root),'push','origin','main','--follow-tags'],text=True)
    checksum=evidence/(apk.name+'.sha256');checksum.write_text(sha+'  '+apk.name+'\n')
    release_exists=subprocess.run(['gh','release','view','v'+args.version,'--repo',args.repo],capture_output=True).returncode==0
    if not release_exists:
        run(['gh','release','create','v'+args.version,str(apk),str(checksum),'--repo',args.repo,
             '--verify-tag','--prerelease','--title',f'v{args.version} — {args.title}',
             '--notes-file',str(args.notes_file)],text=True)
    release=json.loads(run(['gh','api',f'repos/{args.repo}/releases/tags/v{args.version}'],text=True).stdout)
    assert release['prerelease'] and not release['draft']
    asset=next(a for a in release['assets'] if a['name']==apk.name);assert asset['size']==apk.stat().st_size
    tmp=Path(tempfile.mkdtemp(prefix='aeropulse-release-verify-'))
    try:
        run(['gh','release','download','v'+args.version,'--repo',args.repo,'--pattern',apk.name,'--dir',str(tmp)],text=True)
        assert digest(tmp/apk.name)==sha
    finally:shutil.rmtree(tmp)
    remote=json.loads(run(['gh','api',f'repos/{args.repo}/git/ref/tags/v{args.version}'],text=True).stdout)['object']
    if remote['type']=='tag':remote=json.loads(run(['gh','api',f'repos/{args.repo}/git/tags/'+remote['sha']],text=True).stdout)['object']
    assert remote['sha']==commit
    record.update({'source_commit':commit,'release_url':release['html_url'],'remote_download_sha256_verified':True})
    (evidence/'08_GitHub发布核验.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'version':args.version,'commit':commit,'release':release['html_url'],'sha256':sha,'download_verified':True},ensure_ascii=False))

if __name__=='__main__':main()
