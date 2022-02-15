using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Erp;
using Erp.Contracts;
using Erp.Tables;
using Erp.Tablesets;
using Ice;
using Ice.Lib;
using Epicor.Hosting;
using Erp.BO.CRTI_MiscAction.Resources;
using Erp.Internal.Lib;
using Ice.BO;
using System.Data;

namespace Erp.BO.CRTI_MiscAction
{
    /// <summary>
    /// 
    /// </summary>
    internal class Translate
    {
        private HashSet<string> _notfound;
        ErpContext _db;
        Session _session;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="db"></param>
        /// <param name="session"></param>
        internal Translate(Erp.ErpContext db, Session session)
        {
            _db = db;
            _session = session;
            _notfound = new HashSet<string>();
        }

        /// <summary>
        /// 
        /// </summary>
        internal void AddOriginalText(StreamWriter myFile, string program)
        {
            var lastOrgID = GetID() - 1;
            try
            {
                var unfounded = _notfound.ToList();
                using (var txscope = IceDataContext.CreateDefaultTransactionScope())
                {
                    Ice.Contracts.LangTranSvcContract ltsc = Ice.Assemblies.ServiceRenderer.GetService<Ice.Contracts.LangTranSvcContract>(_db);
                    Ice.Tablesets.TranTextTableset ttt = new Ice.Tablesets.TranTextTableset();
                    foreach (var instances in unfounded)
                    {
                        if (!IsDouble(instances))
                        {
                            var row = new Ice.Tablesets.TranTextRow();
                            row.OriginalText = instances;
                            row.TranslatedText = "";
                            row.ProgramID = program;
                            ttt.TranText.Add(row);
                        }
                        ltsc.ImportXml(ttt, true);
                    }
                                       
                    _db.Validate();
                    txscope.Complete();
                }
            }
            catch (Exception e)
            {
                myFile.WriteLine(e.ToString());
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="translations"></param>
        /// <param name="words"></param>
        /// <param name="theProgram"></param>
        /// <param name="languageID"></param>
        internal void GetTranslations(out string translations, string words, string theProgram, string languageID)
        {
            translations = string.Empty;
            using (var myFile = new StreamWriter(new FileStream("c:\\temp\\traduction.txt", FileMode.Create)))
            {
                myFile.WriteLine("<=====Debut-GetTranslations()=====>");
                try
                {
                    var untranslated = FindNonTranslated(words, languageID, myFile);

                    foreach (string s in untranslated)
                    {
                        myFile.WriteLine("Unstranslated : " + s);  
                    }

                    var tran = new Dictionary<string, string>();
                    var list = new List<string>();

                    var Trans = (from translation in _db.LangTran
                                 join program in _db.LangProg on translation.ProgramID equals program.ProgramID
                                 join original in _db.LangOrg on translation.OrgTextID equals original.OrgTextID
                                 where untranslated.Contains(original.OrgText) && translation.LangNameID == languageID
                                 select new { Original = original.OrgText, Tran = translation.TransText }).Union
                                (from translation in _db.LangTran
                                 join original in _db.LangOrg on translation.OrgTextID equals original.OrgTextID
                                 where untranslated.Contains(original.OrgText) && translation.LangNameID == languageID
                                 select new { Original = original.OrgText, Tran = translation.TransText });

                    foreach (var t in Trans)
                    {
                        myFile.WriteLine(t.Original + "::" + t.Tran);
                        if (!tran.ContainsKey(t.Original.ToLower()) && t.Tran.Count() > 0)
                            tran.Add(t.Original.ToLower(), t.Tran);
                    }

                    foreach (var u in untranslated)
                    {
                        if (!tran.ContainsKey(u))
                        {
                            _notfound.Add(u);
                        }
                    }

                    AddOriginalText(myFile, theProgram);

                    foreach (var instances in tran)
                    {
                        list.Add(instances.Key + ":" + instances.Value);
                    }

                    translations = string.Join("|", list.ToArray());
                }
                catch (Exception e)
                {
                    myFile.WriteLine(e.ToString());
                }
                myFile.WriteLine("<=====Fin-GetTranslations()=====>");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private bool IsDouble(string text)
        {
            return (from orgText in _db.LangOrg
                    where orgText.OrgText == text
                    select orgText.OrgTextID).Count() > 0;
        }

        /// <summary>
        /// trims the words to search
        /// </summary>
        /// <param name="words"></param>
        /// <returns></returns>
        private HashSet<string> TrimArray(string words)
        {
            var trimedArray = new HashSet<string>();
            
            foreach (var s in words.Split('|'))
            {
                trimedArray.Add(s);
            }
            
            return trimedArray;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="words"></param>
        /// <param name="lang"></param>
        /// <param name="sw"></param>
        /// <returns></returns>
        private List<string> FindNonTranslated(string words, string lang, StreamWriter sw)
        {
            var searchedFor = TrimArray(words);

            sw.WriteLine("==== FNT ====");
            var CountTran = from Tran in _db.LangTran
                             where searchedFor.Contains(Tran.TransText) && Tran.LangNameID == lang
                             select new { Tran = Tran.TransText };

            foreach (var instances in CountTran)
            {
                sw.WriteLine("CountTran :: " + instances.Tran);
                searchedFor.Remove(instances.Tran);
            }

            return searchedFor.ToList();
        }

        /// <summary>
        /// Gets the last ID in the Table
        /// </summary>
        /// <returns>int First OrgTextID</returns>
        private int GetID()
        {
            var ID = -1;
            var langOrgRow = _db.LangOrg.OrderBy(row => row.OrgTextID).FirstOrDefault();
            
            //If the table is not empty
            if (langOrgRow != null)
                ID = langOrgRow.OrgTextID;
            
            return ID;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="words"></param>
        /// <param name="langID"></param>
        internal void GetAllUntranslated(out string words, string langID)
        {
            IQueryable<Ice.Tables.LangOrg> text = (from org in _db.LangOrg select org)
                                                          .Except<Ice.Tables.LangOrg>
                                                          (from org in _db.LangOrg
                                                          join tran in _db.LangTran on org.OrgTextID equals tran.OrgTextID into J
                                                          from ex in J.DefaultIfEmpty()
                                                          where ex.LangNameID == langID || org.SystemFlag == true
                                                          select org);

            Ice.Tablesets.TranTextTable trantexttable = new Ice.Tablesets.TranTextTable();
            Ice.BO.TranTextDataSet.TranTextDataTable tranText = new Ice.BO.TranTextDataSet.TranTextDataTable();            

            foreach (var instances in text)
            {
                Ice.Tablesets.TranTextRow trantextrow = new Ice.Tablesets.TranTextRow();

                trantextrow.OrgTextID = instances.OrgTextID;
                trantextrow.OriginalText = instances.OrgText;
                trantextrow.LanguageID = langID;

                trantextrow.SetErrorCodeUnspecified();
                trantextrow.SetProgTranTextUnspecified();
                trantextrow.SetProgramIDUnspecified();
                trantextrow.SetTranslatedTextUnspecified();
                trantextrow.SetSysRowIDUnspecified();

                trantexttable.Add(trantextrow);
            }

            DatasetAdapter.CopyTSTableToDataTable<Ice.Tablesets.TranTextRow, Ice.BO.TranTextDataSet.TranTextDataTable>(trantexttable, tranText);

            using (var ms = new MemoryStream())
            {
                tranText.WriteXml(ms);
                words = Encoding.UTF8.GetString(ms.ToArray());

                using (var myFile = new StreamWriter(new FileStream("c:\\temp\\serialization.txt", FileMode.Create)))
                {

                    myFile.WriteLine(text.Count().ToString());
                    myFile.WriteLine(Encoding.UTF8.GetString(ms.ToArray()));                
                }
            }
        }

        /// <summary>
        /// Gets all the strings in the BPM
        /// </summary>
        /// <param name="bpmPath">Path for the BPM</param>
        /// <returns>a string representing a serialized version of a dataset</returns>
        internal string GetBPMStrings(string bpmPath)
        {
            var sf = new MiscActions.StringFinder(bpmPath);
            _notfound.Clear();
            _notfound = sf.GoodString;

            using (var MyFile = new StreamWriter(new FileStream("c:\\temp\\BPM_Strings.txt", FileMode.Create)))
            {
                try
                {
                    AddOriginalText(MyFile, "");
                }
                catch(Exception e)
                {
                    MyFile.WriteLine(e.Message);
                }
            }

            return sf.SerializeWrongsTrans;
        }
    }
}
