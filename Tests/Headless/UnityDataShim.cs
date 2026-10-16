using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
namespace UnityEngine {
 public sealed class TextAsset {public string text;public TextAsset(string s){text=s;}}
 public static class Resources {public static string Root; public static T Load<T>(string key) where T:class {var p=Path.Combine(Root,key+".json");return File.Exists(p)?new TextAsset(File.ReadAllText(p)) as T:null;}}
 public sealed class FieldsOnly:DefaultContractResolver {
  protected override IList<JsonProperty> CreateProperties(Type t,MemberSerialization m){return t.GetFields(BindingFlags.Instance|BindingFlags.Public).Select(f=>{var p=CreateProperty(f,MemberSerialization.Fields);p.Readable=p.Writable=true;return p;}).ToList();}
 }
 public static class JsonUtility {
  static readonly JsonSerializerSettings S=new JsonSerializerSettings{ContractResolver=new FieldsOnly(),FloatParseHandling=FloatParseHandling.Double,Culture=System.Globalization.CultureInfo.InvariantCulture};
  public static string ToJson(object v,bool pretty=false){return JsonConvert.SerializeObject(v,pretty?Formatting.Indented:Formatting.None,S);}
  public static T FromJson<T>(string s){return JsonConvert.DeserializeObject<T>(s,S);}
 }
}
