#!/usr/bin/env python3
"""Read-only final audit of Git tags, built sources and release digests."""
import argparse,hashlib,json,subprocess
from pathlib import Path

def run(*args): return subprocess.run(args,check=True,capture_output=True).stdout

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--expected-visibility',choices=['PUBLIC','PRIVATE'],default='PUBLIC');ap.add_argument('--output',type=Path,required=True);ap.add_argument('--versions',nargs='+',default=['0.10.0','0.11.0','0.12.0','0.13.0','0.14.0']);a=ap.parse_args()
    root=Path(__file__).resolve().parents[1];repo='devmczdj/AeroPulse-Quest'
    info=json.loads(run('gh','api','repos/'+repo));visibility='PRIVATE' if info['private'] else 'PUBLIC';assert visibility==a.expected_visibility,'Repository visibility differs from the requested audit scope'
    result=[]
    for v in a.versions:
        tag='v'+v;commit=run('git','-C',str(root),'rev-parse',tag+'^{commit}').decode().strip()
        data=json.loads(run('git','-C',str(root),'show',tag+':Docs/11_Releases/'+v+'.json'))
        for name,digest in data['source_files_at_build'].items():
            assert hashlib.sha256(run('git','-C',str(root),'show',tag+':'+name)).hexdigest()==digest,(tag,name)
        remote=json.loads(run('gh','api','repos/'+repo+'/git/ref/tags/'+tag))['object']
        if remote['type']=='tag':remote=json.loads(run('gh','api','repos/'+repo+'/git/tags/'+remote['sha']))['object']
        assert remote['sha']==commit
        release=json.loads(run('gh','api','repos/'+repo+'/releases/tags/'+tag))
        apk=next(x for x in release['assets'] if x['name']==data['apk'])
        assert apk['size']==data['size_bytes'] and apk['digest']=='sha256:'+data['sha256']
        checksum=next(x for x in release['assets'] if x['name']==data['apk']+'.sha256')
        # Official API binary endpoint returns checksum bytes with existing gh authentication.
        value=run('gh','api','repos/'+repo+'/releases/assets/'+str(checksum['id']),'-H','Accept: application/octet-stream').decode().split()[0]
        assert value==data['sha256']
        result.append({'tag':tag,'source_commit':commit,'source_files_verified':len(data['source_files_at_build']),'versionCode':data['versionCode'],'apk_sha256':value,'remote_digest_and_checksum_match':True,'release':release['html_url']})
    assert all(result[i]['versionCode']<result[i+1]['versionCode'] for i in range(len(result)-1))
    # Original course documents are outside this software repository and audited by caller.
    report={'repository':repo,'visibility':visibility,'versions':result,'scope':'Git source bytes + remote asset digest/checksum. Per-version full APK redownload receipts are separately retained; no fabricated device tests.'}
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'verified_versions':len(result),'visibility':visibility}))
if __name__=='__main__':main()
