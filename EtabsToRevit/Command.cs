#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace DSB.RevitTools.EtabsToRevit
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            EtabsModel EtabModel = new EtabsModel();
            const string path = @"F:\00 - ETABS\REVIT_Report.xlsx";
            const string pathSea = @"F:\00 - ETABS\2016-08-03-Revit-Etabs.xlsx";
            List<EtabObject> EtabsList = EtabModel.Get_ExcelFileToList(pathSea);

            // Etab Column List Sorted using the X value. 
            List<EtabObject> EtabsColumnList = EtabModel.Get_EtabsColumnList(EtabsList);
            List<EtabObject> Sorted_EtabsColumnList = EtabsColumnList.OrderBy(o => o.Get_StartPoint().X).ToList();
            
            List<EtabObject> EtabsBeamList = EtabModel.Get_EtabsBeamList(EtabsList);
            List<EtabObject> Sorted_EtabsBeamList = EtabsBeamList.OrderBy(o => o.Get_StartPoint().X).ToList();

            // Revit Column and Beam List Sorted by X value
            RevitModelElements revitModelElements = new RevitModelElements();
            revitModelElements.Get_ColumnSymbols(doc);
            revitModelElements.Get_BeamSymbols(doc);
            List<RevitObject> _RevitColumnsList = revitModelElements.Get_RevitColumnsList();
            List<RevitObject> Sorted_RevitColumnsList = _RevitColumnsList.OrderBy(o => o.Get_PointStart().X).ToList();
            List<RevitObject> _RevitFramingList = revitModelElements.Get_RevitFramingList();
            List<RevitObject> Sorted_RevitFramingList = _RevitFramingList.OrderBy(o => o.Get_PointStart().X).ToList();
            
            // Algorithm list for columns start and end points 
            Algorithm algorithmColumnStart = new Algorithm(Sorted_EtabsColumnList, "Start");
            Algorithm algorithmColumnEnd = new Algorithm(Sorted_EtabsColumnList, "End");
            List<Point> pointsColumnStart = algorithmColumnStart.Get_Points();
            List<Point> pointsColumnEnd = algorithmColumnEnd.Get_Points();
            
            // Algorithm list for beams start and end points
            Algorithm algorithmBeamStart = new Algorithm(Sorted_EtabsBeamList, "Start");
            Algorithm algorithmBeamEnd = new Algorithm(Sorted_EtabsBeamList, "End");
            List<Point> pointsBeamStart = algorithmBeamStart.Get_Points();
            List<Point> pointsBeamEnd = algorithmBeamEnd.Get_Points();

            //List of all column symbols "also know as types in Revit"
            var structuralColumnType = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralColumns);
            List<FamilySymbol> structuralColumnTypeList = structuralColumnType.ToElements().Cast<FamilySymbol>().ToList();
            
            //List of all farming symbols "also know as types in Revit"
            var structuralFramingType = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);
            List<FamilySymbol> structuralFramingTypeList = structuralFramingType.ToElements().Cast<FamilySymbol>().ToList();

            ElementClassFilter FamilyFilter = new ElementClassFilter(typeof(Family));
            FilteredElementCollector FamilyCollector = new FilteredElementCollector(doc);
            ICollection<Element> AllFamilies = FamilyCollector.WherePasses(FamilyFilter).ToElements();
            foreach (Family Fmly in AllFamilies)
            {
                string FamilyName = Fmly.Name;
                foreach (Parameter Param in Fmly.Parameters)
                {
                    string ParamName = Param.Definition.Name;
                }
            }

            // Change column element type using Etabs objects 
            RevitChangeType revitChangetype = new RevitChangeType();
            foreach (RevitObject rvtObj in Sorted_RevitColumnsList)
            {
                var startPoint = algorithmColumnStart.Run(rvtObj.Get_PointStart(), pointsColumnStart);
                var endPoint = algorithmColumnEnd.Run(rvtObj.Get_PointEnd(), pointsColumnEnd);

                foreach (Point pStart in startPoint)
                {
                    foreach (Point pEnd in endPoint)
                    {
                        if (pStart.UniqueID == pEnd.UniqueID)
                        {                
                            var EtabsObject = Sorted_EtabsColumnList.Find(item => item.Get_UniqueID() == pStart.UniqueID);
                            revitChangetype.changeType(doc, rvtObj, EtabsObject, structuralColumnTypeList);
                            break;
                        }
                    }
                }
            }

            // Change framing element type using Etabs object
            foreach (RevitObject rvtObj in Sorted_RevitFramingList)
            {
                var startPoint = algorithmBeamStart.Run(rvtObj.Get_PointStart(), pointsBeamStart);
                var endPoint = algorithmBeamEnd.Run(rvtObj.Get_PointEnd(), pointsBeamEnd);

                foreach (Point pStart in startPoint)
                {
                    foreach (Point pEnd in endPoint)
                    {
                        if (pStart.UniqueID == pEnd.UniqueID)
                        {
                            var EtabsObject = Sorted_EtabsBeamList.Find(item => item.Get_UniqueID() == pStart.UniqueID);
                            revitChangetype.changeType(doc, rvtObj, EtabsObject, structuralFramingTypeList);
                            break;
                        }
                    }
                }
            }
            return Result.Succeeded;
        }
    }
}
