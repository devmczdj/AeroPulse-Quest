import hashlib,importlib.util,tempfile,unittest
from pathlib import Path
repo=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('release',repo/'Tools/01_publish_release.py');release=importlib.util.module_from_spec(spec);spec.loader.exec_module(release)
class ReleaseManifestChecks(unittest.TestCase):
 def test_exact_sources_and_digest(self):
  with tempfile.TemporaryDirectory() as directory:
   root=Path(directory)
   for name in ['Assets/Test.cs','Packages/manifest.json','ProjectSettings/ProjectVersion.txt','DESIGN.md']:
    path=root/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(name)
   hashes={str(p.relative_to(root)):release.digest(p) for p in root.rglob('*') if p.is_file()}
   release.verify_source_manifest(root,hashes)
   for missing in list(hashes):
    invalid=dict(hashes);del invalid[missing]
    with self.assertRaisesRegex(RuntimeError,'incomplete'):release.verify_source_manifest(root,invalid)
   invalid=dict(hashes);invalid['../outside']='0'*64
   with self.assertRaisesRegex(RuntimeError,'stale'):release.verify_source_manifest(root,invalid)
   (root/'Assets/Test.cs').write_text('changed after build')
   with self.assertRaisesRegex(RuntimeError,'changed after build'):release.verify_source_manifest(root,hashes)
   (root/'Assets/Test.cs').write_text('Assets/Test.cs')
   (root/'Assets/new.cs').write_text('unlisted runtime file')
   with self.assertRaisesRegex(RuntimeError,'incomplete'):release.verify_source_manifest(root,hashes)
if __name__=='__main__':unittest.main()
