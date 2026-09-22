"""Real managed runtime fixtures, generated only under an explicit isolated evidence root."""
import argparse,json,subprocess,zipfile
from pathlib import Path

def main():
    p=argparse.ArgumentParser();p.add_argument('output',type=Path);a=p.parse_args();root=a.output.resolve();root.mkdir(parents=True,exist_ok=True)
    core=Path(__file__).resolve().parents[1]/'Library/ScriptAssemblies/Flats.Core.dll'
    for slug,version,scope in [('base','1.0.0','ClientOnly'),('session','1.0.0','RequiredForSession'),('session','2.0.0','RequiredForSession'),('extra','1.0.0','RequiredForSession')]:
        mid='verification.'+slug;assembly='Cloud_'+slug+'_'+version.replace('.','_');out=root/assembly;out.mkdir(exist_ok=True)
        deps=[dict(id='verification.base',minimum='1.0.0',maximum='2.0.0')] if slug=='session' else []
        m=dict(schema=1,id=mid,version=version,name='Cloud '+slug,author='FLATS verification',description='Isolated managed runtime fixture. Never publish to players.',category='Verification',changelog='Cloud matrix fixture',gameMinimum='5.3.5',gameMaximum='5.4.0',apiMinimum='1.0.0',apiMaximum='2.0.0',scope=scope,kind='managed',assembly=assembly+'.dll',entryType='CloudFixture',dependencies=deps,conflicts=[])
        dependencies='new ModuleDependency[]{new ModuleDependency("verification.base",new VersionRange("1.0.0","2.0.0"))}' if deps else 'new ModuleDependency[0]'
        (out/'Fixture.cs').write_text('using Flats.Modules; public sealed class CloudFixture:IFirstPartyModule { public static int Active; public ModuleManifest Manifest {get;} = new ModuleManifest("'+mid+'","'+version+'","Cloud fixture","Isolated fixture",ModuleScope.'+scope+',new VersionRange("1.0.0","2.0.0"),new VersionRange("5.3.5","5.4.0"),'+dependencies+'); public void Initialize(ModuleLifetime l){} public void Enable(ModuleLifetime l){l.Own(()=>Active--);Active++;} public void Disable(){} }',encoding='utf-8')
        (out/'Fixture.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><AssemblyName>'+assembly+'</AssemblyName></PropertyGroup><ItemGroup><Reference Include="Flats.Core"><HintPath>'+str(core)+'</HintPath></Reference></ItemGroup></Project>',encoding='utf-8')
        subprocess.run(['dotnet','build',str(out/'Fixture.csproj'),'-c','Release','-v','quiet'],check=True)
        for variant in ['normal','hash'] if slug=='session' and version=='1.0.0' else ['normal']:
            archive=root/f'{slug}-{version}-{variant}.zip'
            with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
                z.writestr('manifest.json',json.dumps(m));z.write(out/'bin/Release/netstandard2.1'/m['assembly'],m['assembly'])
                z.writestr('fixture.txt','Explicit real runtime fixture; '+variant)
    print('Cloud fixture packages generated: '+str(root))
if __name__=='__main__':main()
