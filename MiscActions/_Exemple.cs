using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Erp.Tables;
using Erp.Tablesets;
using Erp.Services.BO;

namespace Erp.BO.CRTI_MiscAction
{
    class Exemple
    {

        Erp.ErpContext Db;
        Epicor.Hosting.Session Session;

        public Exemple(Erp.ErpContext db, Epicor.Hosting.Session session)
        {
            this.Db = db;
            this.Session = session;
        }

        /* Exemple de procédure à inclure dans une customization pour l'appel du MiscAction
         
        public void CallExemple()
        {
            private Adapters.CRTI_MiscActionAdapter adpMiscAction;

            adpMiscAction = new Adapters.CRTI_MiscActionAdapter(this);
            adpMiscAction.BOConnect();

            object oString = null;
            object oInt = null;
            object oDate = null;

            adpMiscAction.CallCustomMethod("Exemple.ProcExemple", out oString, out oInt, out oDate, (object)"String", (object)100, (object)DateTime.Today, (object)true);

            adpMiscAction.Dispose();
        }          
        */

        public void ProcExemple(string iString, int iInt, DateTime iDate, bool iBool, out object oString, out object oInt, out object oDate)
        {
            using (var MyFile = new System.IO.StreamWriter(new System.IO.FileStream("c:\\temp\\Exemple.txt", System.IO.FileMode.Create)))
            {
                MyFile.WriteLine("Debut");

                MyFile.WriteLine("iString: " + iString);
                MyFile.WriteLine("iDate: " + iDate.ToString());
                MyFile.WriteLine("iBool: " + iBool.ToString());
                oDate = DateTime.Today.AddDays(3);

                oString = "Test";
                oInt = 1;
                oDate = DateTime.Today;
                MyFile.WriteLine("Fin");

            }

        }

        /* Exemple de procédure à inclure dans une customization pour l'appel du MiscAction avec une temp-table
                   
        public void CallExempleDataTable()
        {
            Erp.Adapters.CRTI_MiscActionAdapter adpMiscAction;

            adpMiscAction = new Erp.Adapters.CRTI_MiscActionAdapter(ShipViaForm);
            adpMiscAction.BOConnect();

            var builder = new BOXmlBuilder();

            var shipViaRow = builder.CreateBORow("ShipVia");

            shipViaRow.AddValue("Company", "EPIC06");
            shipViaRow.AddValue("ShipViaCode", "001");
            shipViaRow.AddValue("Description", "Desc 001");
            shipViaRow.AddValue("Character01", "Char 01");
            shipViaRow.AddValue("Number01", 100.ToString());
            shipViaRow.AddValue("Date01", DateTime.Now.Date.ToString("yyyy-MM-dd"));

            // Create more rows
            // ...

            string iShipViaTable = builder.BuildXml();
            object oShipViaTable = null;
             
            adpMiscAction.CallCustomMethod("Exemple.ProcExempleDataTable", out oShipViaTable, (object)iShipViaTable);

            var decoder = new BOXmlDecoder((string)oShipViaTable);
 
            foreach (var boRow in decoder.BORows)
            {
                dsDashboard.Tables[0].Rows.Add(boRow.GetString("Company"), 
                                               boRow.GetString("ShipViaCode"),
											   boRow.GetString("Description"),
											   boRow.GetString("Character01"),
											   boRow.GetDecimal("Number01"),
											   boRow.GetDateNullable("Date01"));
           
            }

            adpMiscAction.Dispose();
*/

        public void ProcExempleDataTable(string iShipViaTable, out string oShipViaTable)
        {
            oShipViaTable = "";

                    /* A reviser 

            using (var MyFile = new System.IO.StreamWriter(new System.IO.FileStream("c:\\temp\\Exemple.txt", System.IO.FileMode.Create)))
            {
                oShipViaTable = "";

                var decoder = new CRTI_MiscActionSvc.BOXmlDecoder(iShipViaTable);

                try
                {
                    MyFile.WriteLine("Started : " + DateTime.Now.ToString());
                    var builder = new CRTI_MiscActionSvc.BOXmlBuilder();

                    // Seulement a titre d'exemple, on crée la table à retourner en Output parameter à partir de la table reçu en Input parameter
                    foreach (var boRow in decoder.BORows)
                    {
                        MyFile.WriteLine("Start processing ShipVia Row :");

                        var shipViaRow = builder.CreateBORow("ShipVia");

                        shipViaRow.AddValue("Company", boRow.GetString("Company"));
                        shipViaRow.AddValue("ShipViaCode", boRow.GetString("ShipViaCode"));
                        shipViaRow.AddValue("Description", boRow.GetString("Description"));
                        shipViaRow.AddValue("Character01", boRow.GetString("Character01"));
                        shipViaRow.AddValue("Number01", (boRow.GetDecimal("Number01") + 1).ToString());
                        shipViaRow.AddValue("Date01", ((DateTime)boRow.GetDateNullable("Date01")).ToString("yyyy-MM-dd"));

                    }

                    oShipViaTable = builder.BuildXml();
                }
                catch (Exception ex)
                {
                    MyFile.WriteLine("Error: " + ex.Message);
                }

            }
            */
        }

    }
}
