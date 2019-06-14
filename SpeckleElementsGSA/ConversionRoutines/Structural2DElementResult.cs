﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleCoreGeometryClasses;
using SpeckleElements;

namespace SpeckleElementsGSA
{
  [GSAObject("", new string[] { }, "results", true, false, new Type[] { typeof(GSA2DElement) }, new Type[] { })]
  public class GSA2DElementResult : IGSASpeckleContainer
  {
    public string GWACommand { get; set; }
    public List<string> SubGWACommand { get; set; } = new List<string>();
    public dynamic Value { get; set; } = new Structural2DElementResult();
  }

  public static partial class Conversions
  {
    public static SpeckleObject ToSpeckle(this GSA2DElementResult dummyObject)
    {
      if (Conversions.GSAElement2DResults.Count() == 0)
        return new SpeckleNull();

      if (Conversions.GSAEmbedResults && !GSASenderObjects.ContainsKey(typeof(GSA2DElement)))
        return new SpeckleNull();

      if (Conversions.GSAEmbedResults)
      {
        List<GSA2DElement> elements = GSASenderObjects[typeof(GSA2DElement)].Cast<GSA2DElement>().ToList();

        foreach (KeyValuePair<string, Tuple<int, int, List<string>>> kvp in Conversions.GSAElement2DResults)
        {
          foreach (string loadCase in GSAResultCases)
          {
            if (!GSA.CaseExist(loadCase))
              continue;

            foreach (GSA2DElement element in elements)
            {
              int id = Convert.ToInt32(element.Value.StructuralId);

              if (element.Value.Result == null)
                element.Value.Result = new Dictionary<string, object>();

              var resultExport = GSA.GetGSAResult(id, kvp.Value.Item1, kvp.Value.Item2, kvp.Value.Item3, loadCase, GSAResultInLocalAxis ? "local" : "global");

              if (resultExport == null)
                continue;

              if (!element.Value.Result.ContainsKey(loadCase))
                element.Value.Result[loadCase] = new Structural2DElementResult()
                {
                  Value = new Dictionary<string, object>()
                };

              // Let's split the dictionary into xxx_face and xxx_vertex
              var faceDictionary = resultExport.ToDictionary(
                x => x.Key,
                x => new List<double>() { (x.Value as List<double>).Last() } as object);
              var vertexDictionary = resultExport.ToDictionary(
                x => x.Key,
                x => (x.Value as List<double>).Take((x.Value as List<double>).Count - 1).ToList() as object);

              (element.Value.Result[loadCase] as Structural2DElementResult).Value[kvp.Key + "_face"] = faceDictionary;
              (element.Value.Result[loadCase] as Structural2DElementResult).Value[kvp.Key + "_vertex"] = vertexDictionary;
            }
          }
        }
      }
      else
      {
        GSASenderObjects[typeof(GSA2DElementResult)] = new List<object>();

        List<GSA2DElementResult> results = new List<GSA2DElementResult>();

        string keyword = HelperClass.GetGSAKeyword(typeof(GSA2DElement));

        foreach (KeyValuePair<string, Tuple<int, int, List<string>>> kvp in Conversions.GSAElement2DResults)
        {
          foreach (string loadCase in GSAResultCases)
          {
            if (!GSA.CaseExist(loadCase))
              continue;

            int id = 1;
            int highestIndex = (int)GSA.RunGWACommand("HIGHEST\t" + keyword);

            while (id <= highestIndex)
            {
              if ((int)GSA.RunGWACommand("EXIST\t" + keyword + "\t" + id.ToString()) == 1)
              {
                string record = GSA.GetGWARecords("GET," + keyword + "," + id.ToString())[0];

                string[] pPieces = record.ListSplit(",");
                if (pPieces[4].ParseElementNumNodes() != 3 && pPieces[4].ParseElementNumNodes() != 4)
                {
                  id++;
                  continue;
                }

                var resultExport = GSA.GetGSAResult(id, kvp.Value.Item1, kvp.Value.Item2, kvp.Value.Item3, loadCase, GSAResultInLocalAxis ? "local" : "global");

                if (resultExport == null)
                {
                  id++;
                  continue;
                }

                // Let's split the dictionary into xxx_face and xxx_vertex
                var faceDictionary = resultExport.ToDictionary(
                  x => x.Key,
                  x => new List<double>() { (x.Value as List<double>).Last() } as object);
                var vertexDictionary = resultExport.ToDictionary(
                  x => x.Key,
                  x => (x.Value as List<double>).Take((x.Value as List<double>).Count - 1).ToList() as object);
                
                var existingRes = results.FirstOrDefault(x => x.Value.TargetRef == id.ToString());
                if (existingRes == null)
                {
                  Structural2DElementResult newRes = new Structural2DElementResult()
                  {
                    Value = new Dictionary<string, object>(),
                    TargetRef = id.ToString(),
                    IsGlobal = !GSAResultInLocalAxis,
                  };
                  newRes.Value[kvp.Key + "_face"] = faceDictionary;
                  newRes.Value[kvp.Key + "_vertex"] = vertexDictionary;

                  newRes.GenerateHash();

                  results.Add(new GSA2DElementResult() { Value = newRes });
                }
                else
                {
                  existingRes.Value.Value[kvp.Key + "_face"] = faceDictionary;
                  existingRes.Value.Value[kvp.Key + "_vertex"] = vertexDictionary;
                }
              }
              id++;
            }
          }
        }

        GSASenderObjects[typeof(GSA2DElementResult)].AddRange(results);
      }

      return new SpeckleObject();
    }
  }
}
