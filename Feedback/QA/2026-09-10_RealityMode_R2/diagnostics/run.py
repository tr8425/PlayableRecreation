from pathlib import Path
import subprocess,sys,time,uuid,shutil
sys.stdout.reconfigure(encoding='utf-8')
r=Path(__file__).parent
name='Command'+uuid.uuid4().hex[:8]
src=Path(sys.argv[1]).resolve()
managed=Path('C:/Program Files (x86)/Steam/steamapps/common/RimWorld/RimWorldWin64_Data/Managed')
refs=[managed/n for n in ['Assembly-CSharp.dll','UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.TextRenderingModule.dll']]
refs += [Path('C:/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/Playable Recreation/Assemblies/PlayableRecreation.dll'),r/'bin/Release/net472/PRQAObserver.dll']
proj='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net472</TargetFramework><LangVersion>latest</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>'+name+'</AssemblyName><GenerateAssemblyInfo>false</GenerateAssemblyInfo></PropertyGroup><ItemGroup><Compile Include="'+str(src)+'"/><PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all"/>'
for ref in refs:proj+='<Reference Include="'+ref.stem+'"><HintPath>'+str(ref)+'</HintPath><Private>false</Private></Reference>'
proj+='</ItemGroup></Project>'
(r/'Command.csproj').write_text(proj,encoding='utf-8')
p=subprocess.run(['dotnet','build',str(r/'Command.csproj'),'-c','Release','--nologo','-v','quiet'],capture_output=True)
if p.returncode: print(p.stdout.decode('utf-8',errors='replace'));sys.exit(p.returncode)
out=r/'result.txt'
if out.exists():out.unlink()
(r/'command.txt').write_text(str(r/'bin/Release/net472'/f'{name}.dll'),encoding='utf-8')
for _ in range(100):
 if out.exists():
  text=out.read_text(encoding='utf-8');print(text);(r/(src.stem+'-'+name+'.result.txt')).write_text(text,encoding='utf-8');break
 time.sleep(.2)
else:print('WAITING: command queued')
