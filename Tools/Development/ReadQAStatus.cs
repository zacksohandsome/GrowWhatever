using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

var context=JObject.Parse(File.ReadAllText("docs/local/validation/qa-context.json"));
string smoke=(string)context["standaloneSlot"];
string trial=smoke.Replace("qa-standalone-","qa-playthrough-");
var output=new JObject();
foreach(var pair in new[]{new{key="standalone",file=smoke+".result.json"},new{key="playthrough",file=trial+".playthrough.json"}})
{
    string path=Path.Combine(Application.persistentDataPath,pair.file);
    if(!File.Exists(path)){output[pair.key]="not yet reported";continue;}
    var result=JObject.Parse(File.ReadAllText(path));
    output[pair.key]=result;
    File.WriteAllText("docs/local/validation/"+pair.key+"-result.json",result.ToString());
}
return output;
