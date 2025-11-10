using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PaperLibrary.Commands;
using PaperLibrary.Extensions;
using PaperLibrary.Utilities;
using PaperLibrary.Utilities.RevitUtilities;
using PaperLibrary.WPF.Templates;

namespace PaperLibrary.Examples.CeilingGrid
{
    [Transaction(TransactionMode.Manual)]
    public class ExtractCeilingGridCmd : BaseIExternalCommand
    {

        public override Result ExecuteCommand(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = commandData.Application.ActiveUIDocument;
            var doc = uiDoc.Document;
            AdvancedProgressBar advancedProgressBar = AdvancedProgressBar.GetInstance();
            try
            {

                List<Element> eles = uiDoc.PickElements_Paper(e => e is Ceiling, PickElementsOptionFactory.CreateCurrentDocumentOption());
                if (!eles.Any()) return Result.Failed;

                advancedProgressBar.Show();             
                advancedProgressBar.AddProcess("Get grids ceiling");

                using (Transaction trans = new(doc, "Get grids ceiling"))
                {
                    trans.Start();
                    ProgressUtility.ExecuteWithProgressBar(advancedProgressBar, eles, "Get grids ceiling", (ele) =>
                    {
                        try
                        {
                            if (ele is Ceiling ceiling)
                            {
                                List<Curve> curves = CeilingUtility.GetCeilingGridLines(ceiling, true);
                                foreach (var curve in curves)
                                {
                                    curve.Visualize_Paper(doc);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            FileUtility.LogProgress($"Num: {ele.Id} - Error: {ex.Message}");
                        }
                    });

                    trans.Commit();
                }
                if (!advancedProgressBar!.AnyLog()) advancedProgressBar.Close();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                advancedProgressBar?.Dispose();
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
            return Result.Succeeded;
        }
    }
}