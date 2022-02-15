using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Data;
using System.Xml;
using System.Xml.Serialization;
namespace Erp.BO.CRTI_MiscAction.MiscActions
{
    /// <summary>
    /// 
    /// </summary>
    public static class DirectoryMapper
    {
        /// <summary>
        /// Gets a map of all the directories
        /// </summary>
        /// <param name="server_path">Path of the server</param>
        /// <returns>A dictionnary representing all the directories with their root as key</returns>
        public static Dictionary<string, List<string>> getMap(string server_path)
        {
            string[] BPM_PATH = { "BO", "DT", "UBAQ" };
            
            BPM_PATH = initArray(BPM_PATH, server_path);
            List<string> BPM_DIRECTORIES = getAllBPM(BPM_PATH);

            return findAllIteration(BPM_DIRECTORIES);
        }

        /// <summary>
        /// List All the BPM
        /// </summary>
        /// <param name="server_path">path of the server</param>
        /// <returns>a list of all the BPM</returns>
        public static List<string> listBPM(string server_path)
        {
            string[] BPM_PATH = { "BO", "DT", "UBAQ" };

            BPM_PATH = initArray(BPM_PATH, server_path);
            return getAllBPM(BPM_PATH);
        }

        /// <summary>
        /// counts all the direcctories
        /// </summary>
        /// <param name="path">The path</param>
        /// <returns>the number of directories</returns>
        public static int countDirectories(string path)
        {
            return Directory.GetDirectories(path).Count();
        }

        /// <summary>
        /// Initialize the base dir array
        /// </summary>
        /// <param name="subdir"></param>
        /// <param name="srv_path"></param>
        /// <returns>The array that has been initialized</returns>
        internal static string[] initArray(string[] subdir, string srv_path)
        {
            int subdir_length = subdir.Length;

            for(int array_index = 0; array_index < subdir_length; array_index++ )
            {
                subdir[array_index] = srv_path + "\\" + subdir[array_index];
            }

            return subdir;
        }

        /// <summary>
        /// Gets all the BPM Needed
        /// </summary>
        /// <param name="directories">
        /// The root of all the BPM
        /// </param>
        /// <returns>
        /// A list of all the existing BPM on that server
        /// </returns>
        private static List<string> getAllBPM(string[] directories)
        {
            List<string> subdir = new List<string>();
            foreach(string directory in directories)
            {
                foreach (string BPM in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    subdir.Add(BPM);
                }
            }

            return subdir;
        }

        /// <summary>
        /// gets all the iterations of a BPM
        /// </summary>
        /// <param name="bpm">gets all the iterations in a program</param>
        /// <returns>a dictionnary representing all the iterations with the parent directory as key</returns>
        private static Dictionary<string, List<string>> findAllIteration(List<string> bpm)
        {
            Dictionary<string , List<string>> iterations = new Dictionary<string,List<string>>();
            foreach(string itr in bpm)
            {
                iterations.Add(itr, findAllSubDirectories(itr));
            }

            return iterations;
        }

        /// <summary>
        /// Gets all the subdirecctories
        /// </summary>
        /// <param name="path">path of the direcctory</param>
        /// <returns>a list of subdirectories</returns>
        public static List<string> findAllSubDirectories(string path)
        {
            return (from subdirectory in Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                   where Directory.GetDirectories(subdirectory).Length == 0
                   select subdirectory).ToList();
        }
    }

    /// <summary>
    /// Finds all the file in a directory
    /// </summary>
    public static class FileMapper
    {
        /// <summary>
        /// Creates a directory of all the BPM with their most recent files
        /// </summary>
        /// <param name="server_path">Path of the server</param>
        /// <returns>Dictionnary with all the strings that represents a map of the files</returns>
        public static Dictionary<string, List<string>> MapFiles(string server_path)
        {
            Dictionary<string, List<string>> FileList = new Dictionary<string, List<string>>();
            List<string> BPM_LIST = DirectoryMapper.listBPM(server_path);


            foreach(string BPM in BPM_LIST)
            {
                if(DirectoryMapper.countDirectories(BPM) < 2)
                {
                    FileList.Add(BPM, Directory.GetFiles(Directory.GetDirectories(BPM)[0]).ToList());
                }
                else
                {
                    string directory = newestDirectory(BPM);
                    FileList.Add(BPM, Directory.GetFiles(directory).ToList());
                }
            }
            return FileList;
        }

        /// <summary>
        /// Gets the newest directoyr
        /// </summary>
        /// <param name="path">BPM Path</param>
        /// <returns>A string of the newest directory</returns>
        private static string newestDirectory(string path)
        {
            List<string> allSubDir = DirectoryMapper.findAllSubDirectories(path);
            DateTime newest_date = DateTime.MinValue;
            string newest_directory = "";

            foreach(string iteration in allSubDir)
            {
                //Find the oldest algorithm
                string firstfile = Directory.GetFiles(iteration)[0];
                if (newest_date < File.GetCreationTimeUtc(firstfile))
                {
                    newest_date = File.GetCreationTimeUtc(firstfile);
                    newest_directory = iteration;
                }
            }
            return newest_directory;
        }
    }

    /// <summary>
    /// Class to find strings in all the BPM
    /// </summary>
    public class StringFinder
    {
        private Dictionary<string, List<string>> filemap;
        private DataTable wrongTranslations = new DataTable("Wrong Translations");
        private HashSet<string> goodTrans = new HashSet<string>();
        private string Logger;

        /// <summary>
        /// Privatising the base constructor
        /// </summary>
        private StringFinder()
        { }

        /// <summary>
        /// Constructor with parameters
        /// </summary>
        /// <param name="server_path">The path of the server</param>
        public StringFinder(string server_path)
        {
            filemap = FileMapper.MapFiles(server_path);
            Logger = "";
            string[] keys = filemap.Keys.ToArray();

            wrongTranslations.Columns.Add("File", typeof(string));
            wrongTranslations.Columns.Add("String", typeof(string));
            wrongTranslations.Columns.Add("Line", typeof(int));

            foreach(string key in keys)
            {
                Logger += "<key>\n" +"  <name>"+ key +"</name>\n" ;  
                foreach(string file in filemap[key])
                {
                    parseSingleFile(file);
                    Logger += "  <file>" + file + "</file>\n";
                }
                Logger += "</key>\n";
            }

            Logging(Logger);
        }

        /// <summary>
        /// Parse a single file To verify all the strings
        /// </summary>
        /// <param name="path">The path of the file</param>
        public void parseSingleFile(string path)
        {
            string[] theFile = System.IO.File.ReadAllLines(path);
            int number_of_lines = theFile.Length;
            Regex wrongTrans = new Regex("(\\+\")|(\"\\+)");

            for(int line = 0; line < number_of_lines; line++ )
            {
                List<string> functions = findTranslateFunction(theFile[line]);
                
                foreach(string s in functions)
                {
                    if(wrongTrans.IsMatch(s))
                    {
                        //Add to wrong dataTable
                        DataRow row = wrongTranslations.NewRow();
                        row["File"] = path;
                        row["Line"] = line + 1;
                        row["String"] = s;

                        wrongTranslations.Rows.Add(row);

                        Logger += "  <Error>" + path + "   <line> " + (line + 1).ToString() + "</line>\n" + "   <string> " + s + "</string>\n  </Error>";
                    }
                    else
                    {
                        //Add to good List
                        goodTrans.Add(getPossibleTranslation(s));
                        
                        Logger += "  <good>" + getPossibleTranslation(s) + "</good>";
                    }
                    Logger += "\n";
                }
            }
        }

        /// <summary>
        /// Get all the translation function in a string
        /// </summary>
        /// <param name="line">the tet of the line</param>
        /// <returns>A list of all the strings</returns>
        public List<string> findTranslateFunction(string line)
        {
            List<string> function = new List<string>();
            Regex functionMatches = new Regex("\\(Ice.Resources.Translation.TranslateString\\(Db,.*\\);");
            MatchCollection collection = functionMatches.Matches(line);
            
            foreach(Match match in collection)
            {
                function.Add(match.Value);
            }

            return function;
        }

        /// <summary>
        /// Trims all the unnessary characters in a string
        /// </summary>
        /// <param name="function">The function to trim</param>
        /// <returns>A trimed string</returns>
        public string getPossibleTranslation(string function)
        {
            Regex translateMatch = new Regex(",.*\",");
            Regex stringFormat = new Regex("\".*\"");
            string trim = translateMatch.Match(function).Value.Trim(',', ' ');

            return stringFormat.Match(trim).Value.Trim('"');
        }

        /// <summary>
        /// Loggin Function
        /// </summary>
        /// <param name="toLog"></param>
        private static void Logging(string toLog)
        {
            using (var MyFile = new System.IO.StreamWriter(new System.IO.FileStream("c:\\temp\\directory.txt", System.IO.FileMode.Create)))
            {
                MyFile.WriteLine(toLog);
            }
        }
        
        /// <summary>
        /// The Dataset reprensenting all the wrongly written translations
        /// </summary>
        public DataTable WrongTranslations
        {
            get
            {
                return this.wrongTranslations;
            }
        }

        /// <summary>
        /// A serialized version of the dataTable
        /// </summary>
        public String SerializeWrongsTrans
        {
            get
            {
                if(this.wrongTranslations == null)
                {
                    return new DataSet("Bad Translations").GetXml();
                }
                else
                {
                    DataSet Serialization = new DataSet("Bad Translations");
                    Serialization.Tables.Add(this.wrongTranslations.Copy());

                    return Serialization.GetXml();
                }
            }
        }

        /// <summary>
        /// All the good strings
        /// </summary>
        public HashSet<string> GoodString
        {
            get
            {
                return this.goodTrans;
            }
        }
        
    }
}