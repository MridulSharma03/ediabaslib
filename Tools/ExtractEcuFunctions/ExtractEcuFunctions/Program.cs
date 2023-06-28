﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using BmwFileReader;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace ExtractEcuFunctions
{
    static class Program
    {
        const string DbPassword = "6505EFBDC3E5F324";

        public class DbInfo
        {
            public DbInfo(string version, DateTime dateTime)
            {
                Version = version;
                DateTime = dateTime;
            }

            public string Version { get; set; }

            public DateTime DateTime { get; set; }
        }

        private static List<string> LangList = new List<string>
        {
            "de", "en", "fr", "th",
            "sv", "it", "es", "id",
            "ko", "el", "tr", "zh",
            "ru", "nl", "pt", "ja",
            "cs", "pl",
        };

        private const string SqlPreOpItems =
                "PREPARINGOPERATORTEXT_DEDE, PREPARINGOPERATORTEXT_ENGB, PREPARINGOPERATORTEXT_ENUS, " +
                "PREPARINGOPERATORTEXT_FR, PREPARINGOPERATORTEXT_TH, PREPARINGOPERATORTEXT_SV, " +
                "PREPARINGOPERATORTEXT_IT, PREPARINGOPERATORTEXT_ES, PREPARINGOPERATORTEXT_ID, " +
                "PREPARINGOPERATORTEXT_KO, PREPARINGOPERATORTEXT_EL, PREPARINGOPERATORTEXT_TR, " +
                "PREPARINGOPERATORTEXT_ZHCN, PREPARINGOPERATORTEXT_RU, PREPARINGOPERATORTEXT_NL, " +
                "PREPARINGOPERATORTEXT_PT, PREPARINGOPERATORTEXT_ZHTW, PREPARINGOPERATORTEXT_JA, " +
                "PREPARINGOPERATORTEXT_CSCZ, PREPARINGOPERATORTEXT_PLPL";

        private const string SqlProcItems =
                "PROCESSINGOPERATORTEXT_DEDE, PROCESSINGOPERATORTEXT_ENGB, PROCESSINGOPERATORTEXT_ENUS, " +
                "PROCESSINGOPERATORTEXT_FR, PROCESSINGOPERATORTEXT_TH, PROCESSINGOPERATORTEXT_SV, " +
                "PROCESSINGOPERATORTEXT_IT, PROCESSINGOPERATORTEXT_ES, PROCESSINGOPERATORTEXT_ID, " +
                "PROCESSINGOPERATORTEXT_KO, PROCESSINGOPERATORTEXT_EL, PROCESSINGOPERATORTEXT_TR, " +
                "PROCESSINGOPERATORTEXT_ZHCN, PROCESSINGOPERATORTEXT_RU, PROCESSINGOPERATORTEXT_NL, " +
                "PROCESSINGOPERATORTEXT_PT, PROCESSINGOPERATORTEXT_ZHTW, PROCESSINGOPERATORTEXT_JA, " +
                "PROCESSINGOPERATORTEXT_CSCZ, PROCESSINGOPERATORTEXT_PLPL";

        private const string SqlPostOpItems =
                "POSTOPERATORTEXT_DEDE, POSTOPERATORTEXT_ENGB, POSTOPERATORTEXT_ENUS, " +
                "POSTOPERATORTEXT_FR, POSTOPERATORTEXT_TH, POSTOPERATORTEXT_SV, " +
                "POSTOPERATORTEXT_IT, POSTOPERATORTEXT_ES, POSTOPERATORTEXT_ID, " +
                "POSTOPERATORTEXT_KO, POSTOPERATORTEXT_EL, POSTOPERATORTEXT_TR, " +
                "POSTOPERATORTEXT_ZHCN, POSTOPERATORTEXT_RU, POSTOPERATORTEXT_NL, " +
                "POSTOPERATORTEXT_PT, POSTOPERATORTEXT_ZHTW, POSTOPERATORTEXT_JA, " +
                "POSTOPERATORTEXT_CSCZ, POSTOPERATORTEXT_PLPL";

        public enum VehicleCharacteristic : long
        {
            Motor = 40142338L,
            Karosserie = 40146178L,
            Baureihe = 40140418L,
            Lenkung = 40141954L,
            Hubraum = 40142722L,
            Getriebe = 40141186L,
            VerkaufsBezeichnung = 40143490L,
            Typ = 40139650L,
            Antrieb = 40143874L,
            BrandName = 40144642L,
            Leistungsklasse = 40141570L,
            Ueberarbeitung = 40145794L,
            Prodart = 40140034L,
            Ereihe = 40140802L,
            Land = 40146562L,
            Tueren = 40144258L,
            Abgas = 68771233282L,
            Motorarbeitsverfahren = 68771234050L,
            Drehmoment = 68771234434L,
            Hybridkennzeichen = 68771233666L,
            Produktlinie = 40039947266L,
            Kraftstoffart = 40143106L,
            MOTKraftstoffart = 99999999909L,
            BasicType = 99999999905L,
            Baureihenverbund = 99999999950L,
            Sicherheitsrelevant = 40145410L,
            MOTEinbaulage = 99999999910L,
            MOTBezeichnung = 99999999918L,
            AELeistungsklasse = 99999999907L,
            AEUeberarbeitung = 99999999908L,
            AEKurzbezeichnung = 99999999906L,
            BaustandsJahr = -100L,
            BaustandsMonat = -101L,
            EMOTBaureihe = 99999999880L,
            EMOTArbeitsverfahren = 99999999878L,
            EMOTDrehmoment = 99999999876L,
            EMOTLeistungsklasse = 99999999874L,
            EMOTUeberarbeitung = 99999999872L,
            EMOTBezeichnung = 99999999870L,
            EMOTKraftstoffart = 99999999868L,
            EMOTEinbaulage = 99999999866L,
            CountryOfAssembly = 99999999851L,
            BaseVersion = 99999999850L,
            ElektrischeReichweite = 99999999854L,
            AEBezeichnung = 99999999848L,
            EngineLabel2 = 99999999701L,
            Engine2 = 99999999702L,
            HeatMOTPlatzhalter1 = 99999999703L,
            HeatMOTPlatzhalter2 = 99999999704L,
            HeatMOTFortlaufendeNum = 99999999705L,
            HeatMOTLeistungsklasse = 99999999706L,
            HeatMOTLebenszyklus = 99999999707L,
            HeatMOTKraftstoffart = 99999999708L,
            KraftstoffartEinbaulage = 53330059L
        }

        private static readonly HashSet<string> FaultCodeLabelIdHashSet = new HashSet<string>();
        private static readonly HashSet<string> FaultModeLabelIdHashSet = new HashSet<string>();
        private static readonly HashSet<string> EnvCondLabelIdHashSet = new HashSet<string>();
        private static Dictionary<string, long> RootClassDict;
        private static string TypeKeyClassId = string.Empty;
        private static string EnvDiscreteNodeClassId = string.Empty;

        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
            TextWriter outTextWriter = args.Length >= 0 ? Console.Out : null;
            TextWriter logTextWriter = args.Length >= 3 ? Console.Out : null;

            if (args.Length < 1)
            {
                outTextWriter?.WriteLine("No Database specified");
                return 1;
            }
            if (args.Length < 2)
            {
                outTextWriter?.WriteLine("No output directory specified");
                return 1;
            }

            string outDir = args[1];
            if (string.IsNullOrEmpty(outDir))
            {
                outTextWriter?.WriteLine("Output directory empty");
                return 1;
            }

            string ecuName = null;
            if (args.Length >= 3)
            {
                ecuName = args[2];
            }

            try
            {
                string outDirSub = Path.Combine(outDir, "EcuFunctions");
                string zipFile = Path.Combine(outDir, "EcuFunctions.zip");
                try
                {
                    if (Directory.Exists(outDirSub))
                    {
                        Directory.Delete(outDirSub, true);
                        Thread.Sleep(1000);
                    }
                    Directory.CreateDirectory(outDirSub);
                }
                catch (Exception)
                {
                    // ignored
                }

                try
                {
                    if (File.Exists(zipFile))
                    {
                        File.Delete(zipFile);
                    }
                }
                catch (Exception)
                {
                    // ignored
                }

                string connection = "Data Source=\"" + args[0] + "\";";
                if (!InitGlobalData(connection))
                {
                    outTextWriter?.WriteLine("Init failed");
                    return 1;
                }

                Dictionary<string, List<string>> typeKeyInfoList = new Dictionary<string, List<string>>();
                int infoIndex = 0;
                foreach (KeyValuePair<string, long> rootClassPair in RootClassDict)
                {
                    if (!ExtractTypeKeyClassInfo(outTextWriter, connection, rootClassPair.Value, typeKeyInfoList, infoIndex))
                    {
                        outTextWriter?.WriteLine("ExtractTypeKeyClassInfo Index: {0} failed", infoIndex);
                        return 1;
                    }

                    infoIndex++;
                }

                if (!WriteTypeKeyClassInfo(outTextWriter, typeKeyInfoList, outDirSub))
                {
                    outTextWriter?.WriteLine("WriteTypeKeyClassInfo failed");
                    return 1;
                }

                if (!WriteVinRanges(outTextWriter, connection, outDirSub))
                {
                    outTextWriter?.WriteLine("Write VinRanges failed");
                    return 1;
                }

                //return 0;

                List<String> ecuNameList;
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (string.IsNullOrEmpty(ecuName))
                {
                    ecuNameList = GetEcuNameList(connection);
                    if (ecuNameList == null)
                    {
                        outTextWriter?.WriteLine("Creating ECU name list failed");
                        return 1;
                    }
                }
                else
                {
                    ecuNameList = new List<string> { ecuName };
                }

                List<Thread> threadList = new List<Thread>();
                foreach (string name in ecuNameList)
                {
                    // limit number of active tasks
                    for (; ; )
                    {
                        threadList.RemoveAll(thread => !thread.IsAlive);
                        int activeThreads = threadList.Count(thread => thread.IsAlive);
                        if (activeThreads < 16)
                        {
                            break;
                        }
                        Thread.Sleep(200);
                    }

                    Thread serializeThread = new Thread(() =>
                    {
                        SerializeEcuFunction(outTextWriter, logTextWriter, connection, outDirSub, name);
                    });
                    serializeThread.Start();
                    threadList.Add(serializeThread);
                }

                foreach (Thread processThread in threadList)
                {
                    processThread.Join();
                }

                List<Thread> threadListFaultData = new List<Thread>();
                foreach (string language in LangList)
                {
                    Thread serializeThread = new Thread(() =>
                    {
                        SerializeEcuFaultData(outTextWriter, logTextWriter, connection, outDirSub, language);
                    });
                    serializeThread.Start();
                    threadListFaultData.Add(serializeThread);
                }

                foreach (Thread processThread in threadListFaultData)
                {
                    processThread.Join();
                }

                if (!CreateZipFile(outDirSub, zipFile))
                {
                    outTextWriter?.WriteLine("Create ZIP failed");
                    return 1;
                }
            }
            catch (Exception e)
            {
                outTextWriter?.WriteLine(e);
            }
            return 0;
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        // ReSharper disable once UnusedParameter.Local
        private static bool SerializeEcuFaultData(TextWriter outTextWriter, TextWriter logTextWriter, string connection, string outDirSub, string language)
        {
            try
            {
                using (SQLiteConnection mDbConnection = new SQLiteConnection(connection))
                {
                    mDbConnection.SetPassword(DbPassword);
                    mDbConnection.Open();

                    outTextWriter?.WriteLine("*** Fault data {0} ***", language);
                    DbInfo dbInfo = GetDbInfo(mDbConnection);
                    EcuFunctionStructs.EcuFaultData ecuFaultData = new EcuFunctionStructs.EcuFaultData
                    {
                        DatabaseVersion = dbInfo.Version,
                        DatabaseDate = dbInfo.DateTime,
                        EcuFaultCodeLabelList = GetFaultCodeLabels(mDbConnection, language),
                        EcuFaultModeLabelList = GetFaultModeLabels(mDbConnection, language),
                        EcuEnvCondLabelList = GetEnvCondLabels(mDbConnection, language)
                    };
                    //logTextWriter?.WriteLine(ecuFaultData);

                    string xmlFile = Path.Combine(outDirSub, "faultdata_" + language + ".xml");
                    XmlSerializer serializer = new XmlSerializer(ecuFaultData.GetType());
                    XmlWriterSettings settings = new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "\t"
                    };
                    using (XmlWriter writer = XmlWriter.Create(xmlFile, settings))
                    {
                        serializer.Serialize(writer, ecuFaultData);
                    }

                    mDbConnection.Close();
                }

                return true;
            }
            catch (Exception e)
            {
                outTextWriter?.WriteLine(e);
                return false;
            }
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private static bool SerializeEcuFunction(TextWriter outTextWriter, TextWriter logTextWriter, string connection, string outDirSub, string ecuName)
        {
            try
            {
                using (SQLiteConnection mDbConnection = new SQLiteConnection(connection))
                {
                    mDbConnection.SetPassword(DbPassword);
                    mDbConnection.Open();

                    outTextWriter?.WriteLine("*** ECU: {0} ***", ecuName);
                    EcuFunctionStructs.EcuVariant ecuVariant = GetEcuVariantFunctions(outTextWriter, logTextWriter, mDbConnection, ecuName);

                    if (ecuVariant != null)
                    {
                        logTextWriter?.WriteLine(ecuVariant);

                        string xmlFile = Path.Combine(outDirSub, ecuName.ToLowerInvariant() + ".xml");
                        XmlSerializer serializer = new XmlSerializer(ecuVariant.GetType());
                        XmlWriterSettings settings = new XmlWriterSettings
                        {
                            Indent = true,
                            IndentChars = "\t"
                        };
                        using (XmlWriter writer = XmlWriter.Create(xmlFile, settings))
                        {
                            serializer.Serialize(writer, ecuVariant);
                        }
                    }

                    mDbConnection.Close();
                }

                return true;
            }
            catch (Exception e)
            {
                outTextWriter?.WriteLine(e);
                return false;
            }
        }

        private static bool InitGlobalData(string connection)
        {
            try
            {
                string[] rootClassNames = Enum.GetNames(typeof(VehicleCharacteristic));
                RootClassDict = new Dictionary<string, long>();
                foreach (string rootClassName in rootClassNames)
                {
                    if (Enum.TryParse(rootClassName, out VehicleCharacteristic rootClassValue))
                    {
                        long value = (long) rootClassValue;
                        if (value > 0)
                        {
                            RootClassDict.Add(rootClassName, value);
                        }
                    }
                }

                using (SQLiteConnection mDbConnection = new SQLiteConnection(connection))
                {
                    mDbConnection.SetPassword(DbPassword);
                    mDbConnection.Open();

                    TypeKeyClassId = DatabaseFunctions.GetNodeClassId(mDbConnection, @"Typschluessel");
                    EnvDiscreteNodeClassId = DatabaseFunctions.GetNodeClassId(mDbConnection, "EnvironmentalConditionTextDiscrete");

                    mDbConnection.Close();

                    if (string.IsNullOrEmpty(TypeKeyClassId))
                    {
                        return false;
                    }

                    if (string.IsNullOrEmpty(EnvDiscreteNodeClassId))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<string> GetEcuNameList(string connection)
        {
            try
            {
                List<string> ecuNameList;
                using (SQLiteConnection mDbConnection = new SQLiteConnection(connection))
                {
                    mDbConnection.SetPassword(DbPassword);
                    mDbConnection.Open();

                    ecuNameList = GetEcuNameList(mDbConnection);

                    mDbConnection.Close();
                }

                return ecuNameList;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static List<string> GetEcuNameList(SQLiteConnection mDbConnection)
        {
            List<string> ecuNameList = new List<string>();
            string sql = @"SELECT NAME FROM XEP_ECUVARIANTS";
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ecuNameList.Add(reader["NAME"].ToString().Trim());
                    }
                }
            }

            return ecuNameList;
        }

        private static EcuFunctionStructs.EcuTranslation GetTranslation(SQLiteDataReader reader, string prefix = "TITLE", string language = null)
        {
            return new EcuFunctionStructs.EcuTranslation(
                language == null || language.ToLowerInvariant() == "de" ? reader[prefix + "_DEDE"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "en" ? reader[prefix + "_ENUS"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "fr" ? reader[prefix + "_FR"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "th" ? reader[prefix + "_TH"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "sv" ? reader[prefix + "_SV"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "it" ? reader[prefix + "_IT"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "es" ? reader[prefix + "_ES"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "id" ? reader[prefix + "_ID"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "ko" ? reader[prefix + "_KO"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "el" ? reader[prefix + "_EL"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "tr" ? reader[prefix + "_TR"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "zh" ? reader[prefix + "_ZHCN"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "ru" ? reader[prefix + "_RU"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "nl" ? reader[prefix + "_NL"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "pt" ? reader[prefix + "_PT"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "ja" ? reader[prefix + "_JA"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "cs" ? reader[prefix + "_CSCZ"].ToString() : string.Empty,
                language == null || language.ToLowerInvariant() == "pl" ? reader[prefix + "_PLPL"].ToString() : string.Empty
                );
        }

        // from GetCharacteristicsByTypeKeyId
        private static bool ExtractTypeKeyClassInfo(TextWriter outTextWriter, string connection, long rootClassId, Dictionary<string, List<string>> typeKeyInfoList, int infoIndex)
        {
            try
            {
                using (SQLiteConnection mDbConnection = new SQLiteConnection(connection))
                {
                    mDbConnection.SetPassword(DbPassword);
                    mDbConnection.Open();

                    outTextWriter?.WriteLine("*** Extract TypeKeyInfo start ClassId={0} ***", rootClassId);
                    string sql = $"SELECT t.NAME AS TYPEKEY, c.NAME AS VALUE FROM XEP_CHARACTERISTICS t INNER JOIN XEP_VEHICLES v ON (v.TYPEKEYID = t.ID)" +
                                 $" INNER JOIN XEP_CHARACTERISTICS c ON (v.CHARACTERISTICID = c.ID) INNER JOIN XEP_CHARACTERISTICROOTS r ON" +
                                 $" (r.ID = c.PARENTID AND r.NODECLASS = {rootClassId}) WHERE t.NODECLASS = {TypeKeyClassId} ORDER BY TYPEKEY";
                    using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
                    {
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string typeKey = reader["TYPEKEY"].ToString();
                                string typeValue = reader["VALUE"].ToString();

                                if (string.IsNullOrEmpty(typeKey) || string.IsNullOrEmpty(typeValue))
                                {
                                    continue;
                                }

                                if (!typeKeyInfoList.TryGetValue(typeKey, out List<string> storageList))
                                {
                                    storageList = new List<string>();
                                    typeKeyInfoList.Add(typeKey, storageList);
                                }

                                while (storageList.Count < infoIndex + 1)
                                {
                                    storageList.Add(string.Empty);
                                }

                                storageList[infoIndex] = typeValue;
                            }
                        }
                    }

                    mDbConnection.Close();
                }

                outTextWriter?.WriteLine("*** Write TypeKeyInfo done ***");

                return true;
            }
            catch (Exception e)
            {
                outTextWriter?.WriteLine(e);
                return false;
            }
        }

        private static bool WriteTypeKeyClassInfo(TextWriter outTextWriter, Dictionary<string, List<string>> typeKeyInfoList, string outDirSub)
        {
            try
            {
                outTextWriter?.WriteLine("*** Write TypeKeyInfo start ***");
                string typeKeysFile = Path.Combine(outDirSub, "typekey.txt");
                int itemCount = 0;
                using (StreamWriter swTypeKeys = new StreamWriter(typeKeysFile))
                {
                    StringBuilder sbHeader = new StringBuilder();
                    foreach (KeyValuePair<string, long> rootClassPair in RootClassDict)
                    {
                        if (sbHeader.Length > 0)
                        {
                            sbHeader.Append(";");
                        }
                        else
                        {
                            sbHeader.Append("#");
                        }
                        sbHeader.Append(rootClassPair.Key);
                        itemCount++;
                    }
                    swTypeKeys.WriteLine(sbHeader.ToString());

                    foreach (KeyValuePair<string, List<string>> typeKeyPair in typeKeyInfoList)
                    {
                        StringBuilder sbLine = new StringBuilder();
                        sbLine.Append(typeKeyPair.Key);
                        int items = 1;
                        foreach (string value in typeKeyPair.Value)
                        {
                            sbLine.Append(";");
                            sbLine.Append(value);
                            items++;
                        }

                        while (items < itemCount)
                        {
                            sbLine.Append(";");
                            items++;
                        }
                        swTypeKeys.WriteLine(sbLine.ToString());
                    }
                }

                outTextWriter?.WriteLine("*** Write TypeKeys done ***");

                return true;
            }
            catch (Exception e)
            {
                outTextWriter?.WriteLine(e);
                return false;
            }
        }

        private static bool WriteVinRanges(TextWriter outTextWriter, string connection, string outDirSub)
        {
            try
            {
                using (SQLiteConnection mDbConnection = new SQLiteConnection(connection))
                {
                    mDbConnection.SetPassword(DbPassword);
                    mDbConnection.Open();

                    outTextWriter?.WriteLine("*** Write VinRanges start ***");
                    string vinRangeFile = Path.Combine(outDirSub, "vinranges.txt");
                    using (StreamWriter swVinranges = new StreamWriter(vinRangeFile))
                    {
                        string sql = @"SELECT v.VINBANDFROM AS VINBANDFROM, v.VINBANDTO AS VINBANDTO, v.TYPSCHLUESSEL AS TYPEKEY FROM VINRANGES v";
                        using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
                        {
                            using (SQLiteDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    swVinranges.WriteLine(reader["VINBANDFROM"] + "," + reader["VINBANDTO"] + "," + reader["TYPEKEY"]);
                                }
                            }
                        }
                    }

                    mDbConnection.Close();
                }

                outTextWriter?.WriteLine("*** Write VinRanges done ***");

                return true;
            }
            catch (Exception e)
            {
                outTextWriter?.WriteLine(e);
                return false;
            }
        }

        private static DbInfo GetDbInfo(SQLiteConnection mDbConnection)
        {
            DbInfo dbInfo = null;
            string sql = @"SELECT VERSION, CREATIONDATE FROM RG_VERSION";
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string version = reader["VERSION"].ToString().Trim();
                        DateTime dateTime = reader.GetDateTime(1);
                        dbInfo = new DbInfo(version, dateTime);
                        break;
                    }
                }
            }

            return dbInfo;
        }

        private static EcuFunctionStructs.EcuVariant GetEcuVariant(SQLiteConnection mDbConnection, string ecuName)
        {
            EcuFunctionStructs.EcuVariant ecuVariant = null;
            string name = ecuName.ToLowerInvariant();
            if (string.Compare(ecuName, "ews3p", StringComparison.OrdinalIgnoreCase) == 0)
            {
                name = "ews3";
            }

            string sql = string.Format(@"SELECT ID, " + DatabaseFunctions.SqlTitleItems + ", ECUGROUPID FROM XEP_ECUVARIANTS WHERE (lower(NAME) = '{0}')", name);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string groupId = reader["ECUGROUPID"].ToString().Trim();
                        ecuVariant = new EcuFunctionStructs.EcuVariant(reader["ID"].ToString().Trim(),
                            groupId,
                            GetEcuGroupName(mDbConnection, groupId),
                            GetTranslation(reader),
                            GetEcuGroupFunctionIds(mDbConnection, groupId));
                    }
                }
            }

            if (ecuVariant != null)
            {
                EcuFunctionStructs.EcuClique ecuClique = FindEcuClique(mDbConnection, ecuVariant);
                if (ecuClique != null)
                {
                    ecuVariant.EcuClique = ecuClique;
                }
            }
            return ecuVariant;
        }

        private static List<EcuFunctionStructs.EcuFaultCode> GetFaultCodes(SQLiteConnection mDbConnection, string variantId)
        {
            List<EcuFunctionStructs.EcuFaultCode> ecuFaultCodeList = new List<EcuFunctionStructs.EcuFaultCode>();
            // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetXepFaultCodeByEcuVariantId
            string sql = string.Format(@"SELECT ID, CODE, DATATYPE, RELEVANCE FROM XEP_FAULTCODES WHERE ECUVARIANTID = {0}", variantId);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        EcuFunctionStructs.EcuFaultCode ecuFaultCode = new EcuFunctionStructs.EcuFaultCode(
                            reader["ID"].ToString().Trim(),
                            reader["CODE"].ToString(),
                            reader["DATATYPE"].ToString(),
                            reader["RELEVANCE"].ToString());
                        ecuFaultCodeList.Add(ecuFaultCode);
                        EcuFunctionStructs.EcuFaultCodeLabel ecuFaultCodeLabel = GetFaultCodeLabel(mDbConnection, ecuFaultCode);
                        List<EcuFunctionStructs.EcuFaultModeLabel> ecuFaultModeLabelList = GetFaultModeLabelList(mDbConnection, ecuFaultCode);
                        List<EcuFunctionStructs.EcuEnvCondLabel> ecuEnvCondLabelList = GetEnvCondLabelList(mDbConnection, ecuFaultCode, variantId);

                        string ecuFaultLabelId = string.Empty;
                        if (ecuFaultCodeLabel != null)
                        {
                            ecuFaultLabelId = ecuFaultCodeLabel.Id;
                            lock (FaultCodeLabelIdHashSet)
                            {
                                FaultCodeLabelIdHashSet.Add(ecuFaultCodeLabel.Id);
                            }
                        }

                        List<string> ecuFaultModeLabelIdList = new List<string>();
                        if (ecuFaultModeLabelList != null)
                        {
                            foreach (EcuFunctionStructs.EcuFaultModeLabel ecuFaultModeLabel in ecuFaultModeLabelList)
                            {
                                ecuFaultModeLabelIdList.Add(ecuFaultModeLabel.Id);
                                lock (FaultModeLabelIdHashSet)
                                {
                                    FaultModeLabelIdHashSet.Add(ecuFaultModeLabel.Id);
                                }
                            }
                        }

                        List<string> ecuEnvCondLabelIdList = new List<string>();
                        if (ecuEnvCondLabelList != null)
                        {
                            foreach (EcuFunctionStructs.EcuEnvCondLabel ecuEnvCondLabel in ecuEnvCondLabelList)
                            {
                                ecuEnvCondLabelIdList.Add(ecuEnvCondLabel.Id);
                                lock (EnvCondLabelIdHashSet)
                                {
                                    EnvCondLabelIdHashSet.Add(ecuEnvCondLabel.Id);
                                }
                            }
                        }

                        ecuFaultCode.EcuFaultCodeLabelId = ecuFaultLabelId;
                        ecuFaultCode.EcuFaultCodeLabel = ecuFaultCodeLabel;
                        ecuFaultCode.EcuFaultModeLabelList = ecuFaultModeLabelList;
                        ecuFaultCode.EcuFaultModeLabelIdList = ecuFaultModeLabelIdList;
                        ecuFaultCode.EcuEnvCondLabelList = ecuEnvCondLabelList;
                        ecuFaultCode.EcuEnvCondLabelIdList = ecuEnvCondLabelIdList;
                    }
                }
            }

            return ecuFaultCodeList;
        }

        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetFaultLabelXepFaultLabel
        private static List<EcuFunctionStructs.EcuFaultCodeLabel> GetFaultCodeLabels(SQLiteConnection mDbConnection, string language)
        {
            List<EcuFunctionStructs.EcuFaultCodeLabel> ecuFaultCodeLabelList = new List<EcuFunctionStructs.EcuFaultCodeLabel>();
            string sql = @"SELECT ID LABELID, CODE, SAECODE, " + DatabaseFunctions.SqlTitleItems + ", RELEVANCE, DATATYPE " +
                         @"FROM XEP_FAULTLABELS";
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string labelId = reader["LABELID"].ToString().Trim();
                        bool addItem;
                        lock (FaultCodeLabelIdHashSet)
                        {
                            addItem = FaultCodeLabelIdHashSet.Contains(labelId);
                        }

                        if (addItem)
                        {
                            ecuFaultCodeLabelList.Add(new EcuFunctionStructs.EcuFaultCodeLabel(labelId,
                                reader["CODE"].ToString(),
                                reader["SAECODE"].ToString(),
                                GetTranslation(reader, "TITLE", language),
                                reader["RELEVANCE"].ToString(),
                                reader["DATATYPE"].ToString()));
                        }
                    }
                }
            }

            return ecuFaultCodeLabelList;
        }

        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetFaultLabelXepFaultLabel
        private static EcuFunctionStructs.EcuFaultCodeLabel GetFaultCodeLabel(SQLiteConnection mDbConnection, EcuFunctionStructs.EcuFaultCode ecuFaultCode)
        {
            EcuFunctionStructs.EcuFaultCodeLabel ecuFaultCodeLabel = null;
            string sql = string.Format(@"SELECT LABELS.ID LABELID, CODE, SAECODE, " + DatabaseFunctions.SqlTitleItems + ", RELEVANCE, DATATYPE " +
                                       @"FROM XEP_FAULTLABELS LABELS, XEP_REFFAULTLABELS REFLABELS" +
                                       @" WHERE CODE = {0} AND LABELS.ID = REFLABELS.LABELID AND REFLABELS.ID = {1}", ecuFaultCode.Code, ecuFaultCode.Id);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ecuFaultCodeLabel = new EcuFunctionStructs.EcuFaultCodeLabel(reader["LABELID"].ToString().Trim(),
                            reader["CODE"].ToString(),
                            reader["SAECODE"].ToString(),
                            GetTranslation(reader),
                            reader["RELEVANCE"].ToString(),
                            reader["DATATYPE"].ToString());
                        break;
                    }
                }
            }

            return ecuFaultCodeLabel;
        }

        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetFaultModeLabelById
        private static List<EcuFunctionStructs.EcuFaultModeLabel> GetFaultModeLabels(SQLiteConnection mDbConnection, string language)
        {
            List<EcuFunctionStructs.EcuFaultModeLabel> ecuFaultModeLabelList = new List<EcuFunctionStructs.EcuFaultModeLabel>();
            string sql = @"SELECT ID LABELID, CODE, " + DatabaseFunctions.SqlTitleItems + ", RELEVANCE, ERWEITERT " +
                         @"FROM XEP_FAULTMODELABELS ORDER BY LABELID";
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string labelId = reader["LABELID"].ToString().Trim();
                        bool addItem;
                        lock (FaultModeLabelIdHashSet)
                        {
                            addItem = FaultModeLabelIdHashSet.Contains(labelId);
                        }

                        if (addItem)
                        {
                            ecuFaultModeLabelList.Add(new EcuFunctionStructs.EcuFaultModeLabel(labelId,
                                reader["CODE"].ToString(),
                                GetTranslation(reader, "TITLE", language),
                                reader["RELEVANCE"].ToString(),
                                reader["ERWEITERT"].ToString()));
                        }
                    }
                }
            }

            return ecuFaultModeLabelList;
        }

        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetFaultModeLabelById
        private static List<EcuFunctionStructs.EcuFaultModeLabel> GetFaultModeLabelList(SQLiteConnection mDbConnection, EcuFunctionStructs.EcuFaultCode ecuFaultCode)
        {
            List<EcuFunctionStructs.EcuFaultModeLabel> ecuFaultModeLabelList = new List<EcuFunctionStructs.EcuFaultModeLabel>();
            string sql = string.Format(@"SELECT LABELS.ID LABELID, CODE, " + DatabaseFunctions.SqlTitleItems + ", RELEVANCE, ERWEITERT " +
                                       @"FROM XEP_FAULTMODELABELS LABELS, XEP_REFFAULTLABELS REFLABELS" +
                                       @" WHERE LABELS.ID = REFLABELS.LABELID AND REFLABELS.ID = {0} ORDER BY LABELID", ecuFaultCode.Id);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ecuFaultModeLabelList.Add(new EcuFunctionStructs.EcuFaultModeLabel(reader["LABELID"].ToString().Trim(),
                            reader["CODE"].ToString(),
                            GetTranslation(reader),
                            reader["RELEVANCE"].ToString(),
                            reader["ERWEITERT"].ToString()));
                    }
                }
            }

            return ecuFaultModeLabelList;
        }

        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEnvCondLabels
        private static List<EcuFunctionStructs.EcuEnvCondLabel> GetEnvCondLabels(SQLiteConnection mDbConnection, string language)
        {
            List<EcuFunctionStructs.EcuEnvCondLabel> ecuEnvCondLabelList = new List<EcuFunctionStructs.EcuEnvCondLabel>();
            string sql = @"SELECT ID, NODECLASS, " + DatabaseFunctions.SqlTitleItems + ", RELEVANCE, BLOCKANZAHL, UWIDENTTYP, UWIDENT, UNIT " +
                         @"FROM XEP_ENVCONDSLABELS ORDER BY ID";
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string labelId = reader["ID"].ToString().Trim();
                        bool addItem;
                        lock (EnvCondLabelIdHashSet)
                        {
                            addItem = EnvCondLabelIdHashSet.Contains(labelId);
                        }

                        if (addItem)
                        {
                            ecuEnvCondLabelList.Add(new EcuFunctionStructs.EcuEnvCondLabel(labelId,
                                reader["NODECLASS"].ToString(),
                                GetTranslation(reader, "TITLE", language),
                                reader["RELEVANCE"].ToString(),
                                reader["BLOCKANZAHL"].ToString(),
                                reader["UWIDENTTYP"].ToString(),
                                reader["UWIDENT"].ToString(),
                                reader["UNIT"].ToString()));
                        }
                    }
                }
            }

            foreach (EcuFunctionStructs.EcuEnvCondLabel ecuEnvCondLabel in ecuEnvCondLabelList)
            {
                if (string.Compare(ecuEnvCondLabel.NodeClass, EnvDiscreteNodeClassId, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    ecuEnvCondLabel.EcuResultStateValueList = GetResultStateValueList(mDbConnection, ecuEnvCondLabel.Id, language);
                }
            }

            return ecuEnvCondLabelList;
        }

        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEnvCondLabels
        private static List<EcuFunctionStructs.EcuEnvCondLabel> GetEnvCondLabelList(SQLiteConnection mDbConnection,
            EcuFunctionStructs.EcuFaultCode ecuFaultCode, string variantId)
        {
            List<EcuFunctionStructs.EcuEnvCondLabel> ecuEnvCondLabelList = new List<EcuFunctionStructs.EcuEnvCondLabel>();
            string sql = string.Format(@"SELECT ID, NODECLASS, " + DatabaseFunctions.SqlTitleItems + ", RELEVANCE, BLOCKANZAHL, UWIDENTTYP, UWIDENT, UNIT " +
                       @"FROM XEP_ENVCONDSLABELS" +
                       @" WHERE ID IN (SELECT LABELID FROM XEP_REFFAULTLABELS, XEP_FAULTCODES WHERE CODE = {0} AND ECUVARIANTID = {1} AND XEP_REFFAULTLABELS.ID = XEP_FAULTCODES.ID) ORDER BY ID",
                        ecuFaultCode.Code, variantId);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ecuEnvCondLabelList.Add(new EcuFunctionStructs.EcuEnvCondLabel(reader["ID"].ToString().Trim(),
                            reader["NODECLASS"].ToString(),
                            GetTranslation(reader),
                            reader["RELEVANCE"].ToString(),
                            reader["BLOCKANZAHL"].ToString(),
                            reader["UWIDENTTYP"].ToString(),
                            reader["UWIDENT"].ToString(),
                            reader["UNIT"].ToString()));
                    }
                }
            }

            foreach (EcuFunctionStructs.EcuEnvCondLabel ecuEnvCondLabel in ecuEnvCondLabelList)
            {
                if (string.Compare(ecuEnvCondLabel.NodeClass, EnvDiscreteNodeClassId, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    ecuEnvCondLabel.EcuResultStateValueList = GetResultStateValueList(mDbConnection, ecuEnvCondLabel.Id);
                }
            }

            return ecuEnvCondLabelList;
        }

        private static EcuFunctionStructs.EcuClique FindEcuClique(SQLiteConnection mDbConnection, EcuFunctionStructs.EcuVariant ecuVariant)
        {
            if (ecuVariant == null)
            {
                return null;
            }

            string cliqueId = GetRefEcuCliqueId(mDbConnection, ecuVariant.Id);
            if (string.IsNullOrEmpty(cliqueId))
            {
                return null;
            }

            return GetEcuCliqueById(mDbConnection, cliqueId);
        }

        private static EcuFunctionStructs.EcuClique GetEcuCliqueById(SQLiteConnection mDbConnection, string ecuCliqueId)
        {
            if (string.IsNullOrEmpty(ecuCliqueId))
            {
                return null;
            }

            EcuFunctionStructs.EcuClique ecuClique = null;
            string sql = string.Format(@"SELECT ID, CLIQUENKURZBEZEICHNUNG, ECUREPID FROM XEP_ECUCLIQUES WHERE (ID = {0})", ecuCliqueId);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ecuClique = new EcuFunctionStructs.EcuClique(reader["ID"].ToString().Trim(),
                            reader["CLIQUENKURZBEZEICHNUNG"].ToString().Trim(),
                            reader["ECUREPID"].ToString().Trim());
                    }
                }
            }

            if (ecuClique != null)
            {
                string ecuRepsName = GetEcuRepsNameById(mDbConnection, ecuClique.EcuRepId);
                if (!string.IsNullOrEmpty(ecuRepsName))
                {
                    ecuClique.EcuRepsName = ecuRepsName;
                }
            }

            return ecuClique;
        }

        private static string GetRefEcuCliqueId(SQLiteConnection mDbConnection, string ecuRefId)
        {
            if (string.IsNullOrEmpty(ecuRefId))
            {
                return null;
            }

            string cliqueId = null;
            string sql = string.Format(@"SELECT ECUCLIQUEID FROM XEP_REFECUCLIQUES WHERE (ID = {0})", ecuRefId);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cliqueId = reader["ECUCLIQUEID"].ToString().Trim();
                    }
                }
            }

            return cliqueId;
        }

        public static string GetEcuRepsNameById(SQLiteConnection mDbConnection, string ecuId)
        {
            if (string.IsNullOrEmpty(ecuId))
            {
                return null;
            }

            string ecuRepsName = null;
            string sql = string.Format(@"SELECT STEUERGERAETEKUERZEL FROM XEP_ECUREPS WHERE (ID = {0})", ecuId);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ecuRepsName = reader["STEUERGERAETEKUERZEL"].ToString().Trim();
                    }
                }
            }

            return ecuRepsName;
        }


        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEcuGroupFunctionsByEcuGroupId
        private static List<string> GetEcuGroupFunctionIds(SQLiteConnection mDbConnection, string groupId)
        {
            List<string> ecuGroupFunctionIds = new List<string>();
            string sql = string.Format(@"SELECT ID FROM XEP_ECUGROUPFUNCTIONS WHERE ECUGROUPID = {0}", groupId);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ecuGroupFunctionIds.Add(reader["ID"].ToString().Trim());
                    }
                }
            }

            return ecuGroupFunctionIds;
        }

        private static string GetEcuGroupName(SQLiteConnection mDbConnection, string groupId)
        {
            string ecuGroupName = null;
            string sql = string.Format(@"SELECT NAME FROM XEP_ECUGROUPS WHERE ID = {0}", groupId);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ecuGroupName = reader["NAME"].ToString().Trim();
                    }
                }
            }

            return ecuGroupName;
        }

        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetXepNodeClassNameById
        private static string GetNodeClassName(SQLiteConnection mDbConnection, string nodeClass)
        {
            string result = string.Empty;
            string sql = string.Format(@"SELECT NAME FROM XEP_NODECLASSES WHERE ID = {0}", nodeClass);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result = reader["NAME"].ToString().Trim();
                    }
                }
            }

            return result;
        }

        private static List<EcuFunctionStructs.EcuJob> GetFixedFuncStructJobsList(SQLiteConnection mDbConnection, EcuFunctionStructs.EcuFixedFuncStruct ecuFixedFuncStruct)
        {
            List<EcuFunctionStructs.EcuJob> ecuJobList = new List<EcuFunctionStructs.EcuJob>();
            // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEcuJobsWithParameters
            string sql = string.Format(@"SELECT JOBS.ID JOBID, FUNCTIONNAMEJOB, NAME, PHASE, RANK " +
                                       "FROM XEP_ECUJOBS JOBS, XEP_REFECUJOBS REFJOBS WHERE JOBS.ID = REFJOBS.ECUJOBID AND REFJOBS.ID = {0}", ecuFixedFuncStruct.Id);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ecuJobList.Add(new EcuFunctionStructs.EcuJob(reader["JOBID"].ToString().Trim(),
                            reader["FUNCTIONNAMEJOB"].ToString().Trim(),
                            reader["NAME"].ToString().Trim(),
                            reader["PHASE"].ToString(),
                            reader["RANK"].ToString()));
                    }
                }
            }

            foreach (EcuFunctionStructs.EcuJob ecuJob in ecuJobList)
            {
                // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEcuParameters
                List<EcuFunctionStructs.EcuJobParameter> ecuJobParList = new List<EcuFunctionStructs.EcuJobParameter>();
                sql = string.Format(
                    @"SELECT PARAM.ID PARAMID, PARAMVALUE, FUNCTIONNAMEPARAMETER, ADAPTERPATH, NAME, ECUJOBID " +
                    "FROM XEP_ECUPARAMETERS PARAM, XEP_REFECUPARAMETERS REFPARAM WHERE " +
                    "PARAM.ID = REFPARAM.ECUPARAMETERID AND REFPARAM.ID = {0} AND PARAM.ECUJOBID = {1}", ecuFixedFuncStruct.Id, ecuJob.Id);
                using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ecuJobParList.Add(new EcuFunctionStructs.EcuJobParameter(reader["PARAMID"].ToString().Trim(),
                                reader["PARAMVALUE"].ToString().Trim(),
                                reader["ADAPTERPATH"].ToString(),
                                reader["NAME"].ToString().Trim()));
                        }
                    }
                }

                ecuJob.EcuJobParList = ecuJobParList;

                List<EcuFunctionStructs.EcuJobResult> ecuJobResultList = new List<EcuFunctionStructs.EcuJobResult>();
                // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEcuResults
                sql = string.Format(
                    @"SELECT RESULTS.ID RESULTID, " + DatabaseFunctions.SqlTitleItems + ", FUNCTIONNAMERESULT, ADAPTERPATH, NAME, STEUERGERAETEFUNKTIONENRELEVAN, LOCATION, UNIT, UNITFIXED, FORMAT, MULTIPLIKATOR, OFFSET, RUNDEN, ZAHLENFORMAT, ECUJOBID " +
                    "FROM XEP_ECURESULTS RESULTS, XEP_REFECURESULTS REFRESULTS WHERE " +
                    "ECURESULTID = RESULTS.ID AND REFRESULTS.ID = {0} AND RESULTS.ECUJOBID = {1}", ecuFixedFuncStruct.Id, ecuJob.Id);
                using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            EcuFunctionStructs.EcuJobResult ecuJobResult = new EcuFunctionStructs.EcuJobResult(
                                reader["RESULTID"].ToString().Trim(),
                                GetTranslation(reader),
                                reader["FUNCTIONNAMERESULT"].ToString().Trim(),
                                reader["ADAPTERPATH"].ToString(),
                                reader["NAME"].ToString().Trim(),
                                reader["STEUERGERAETEFUNKTIONENRELEVAN"].ToString(),
                                reader["LOCATION"].ToString(),
                                reader["UNIT"].ToString(),
                                reader["UNITFIXED"].ToString(),
                                reader["FORMAT"].ToString(),
                                reader["MULTIPLIKATOR"].ToString(),
                                reader["OFFSET"].ToString(),
                                reader["RUNDEN"].ToString(),
                                reader["ZAHLENFORMAT"].ToString());

                            if (ecuJobResult.EcuFuncRelevant.ConvertToInt() > 0)
                            {
                                ecuJobResultList.Add(ecuJobResult);
                            }
                        }
                    }
                }

                foreach (EcuFunctionStructs.EcuJobResult ecuJobResult in ecuJobResultList)
                {
                    ecuJobResult.EcuResultStateValueList = GetResultStateValueList(mDbConnection, ecuJobResult.Id);
                }

                ecuJob.EcuJobResultList = ecuJobResultList;
            }

            return ecuJobList;
        }

        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEcuResultStateValues
        private static List<EcuFunctionStructs.EcuResultStateValue> GetResultStateValueList(SQLiteConnection mDbConnection, string id, string language = null)
        {
            List<EcuFunctionStructs.EcuResultStateValue> ecuResultStateValueList = new List<EcuFunctionStructs.EcuResultStateValue>();
            string sql = string.Format(@"SELECT ID, " + DatabaseFunctions.SqlTitleItems + ", STATEVALUE, VALIDFROM, VALIDTO, PARENTID " +
                                       "FROM XEP_STATEVALUES WHERE (PARENTID IN (SELECT STATELISTID FROM XEP_REFSTATELISTS WHERE (ID = {0})))", id);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ecuResultStateValueList.Add(new EcuFunctionStructs.EcuResultStateValue(reader["ID"].ToString().Trim(),
                            GetTranslation(reader, "TITLE", language),
                            reader["STATEVALUE"].ToString(),
                            reader["VALIDFROM"].ToString(),
                            reader["VALIDTO"].ToString(),
                            reader["PARENTID"].ToString().Trim()));
                    }
                }
            }

            return ecuResultStateValueList;
        }

        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEcuFixedFunctionsByParentId
        private static List<EcuFunctionStructs.EcuFixedFuncStruct> GetEcuFixedFuncStructList(SQLiteConnection mDbConnection, string parentId)
        {
            List<EcuFunctionStructs.EcuFixedFuncStruct> ecuFixedFuncStructList = new List<EcuFunctionStructs.EcuFixedFuncStruct>();
            string sql = string.Format(@"SELECT ID, NODECLASS, " + DatabaseFunctions.SqlTitleItems + ", " +
                                       SqlPreOpItems + ", " + SqlProcItems + ", " + SqlPostOpItems + ", " +
                                       "SORT_ORDER, ACTIVATION, ACTIVATION_DURATION_MS " +
                                       "FROM XEP_ECUFIXEDFUNCTIONS WHERE (PARENTID = {0})", parentId);
            using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nodeClass = reader["NODECLASS"].ToString();
                        EcuFunctionStructs.EcuFixedFuncStruct ecuFixedFuncStruct = new EcuFunctionStructs.EcuFixedFuncStruct(reader["ID"].ToString().Trim(),
                            nodeClass,
                            GetNodeClassName(mDbConnection, nodeClass),
                            GetTranslation(reader),
                            GetTranslation(reader, "PREPARINGOPERATORTEXT"),
                            GetTranslation(reader, "PROCESSINGOPERATORTEXT"),
                            GetTranslation(reader, "POSTOPERATORTEXT"),
                            reader["SORT_ORDER"].ToString(),
                            reader["ACTIVATION"].ToString(),
                            reader["ACTIVATION_DURATION_MS"].ToString());

                        ecuFixedFuncStruct.EcuJobList = GetFixedFuncStructJobsList(mDbConnection, ecuFixedFuncStruct);
                        ecuFixedFuncStructList.Add(ecuFixedFuncStruct);
                    }
                }
            }

            return ecuFixedFuncStructList;
        }

        // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEcuFixedFunctionsForEcuVariant
        private static EcuFunctionStructs.EcuVariant GetEcuVariantFunctions(TextWriter outTextWriter, TextWriter logTextWriter, SQLiteConnection mDbConnection, string ecuName)
        {
            EcuFunctionStructs.EcuVariant ecuVariant = GetEcuVariant(mDbConnection, ecuName);
            if (ecuVariant == null)
            {
                outTextWriter?.WriteLine("ECU variant not found");
                return null;
            }

            ecuVariant.EcuFaultCodeList = GetFaultCodes(mDbConnection, ecuVariant.Id);
            int faultCodeCount = 0;
            if (ecuVariant.EcuFaultCodeList != null)
            {
                faultCodeCount = ecuVariant.EcuFaultCodeList.Count;
            }

            List<EcuFunctionStructs.RefEcuVariant> refEcuVariantList = new List<EcuFunctionStructs.RefEcuVariant>();
            {
                string sql = string.Format(@"SELECT ID, ECUVARIANTID FROM XEP_REFECUVARIANTS WHERE ECUVARIANTID = {0}", ecuVariant.Id);
                using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            refEcuVariantList.Add(new EcuFunctionStructs.RefEcuVariant(reader["ID"].ToString(),
                                reader["ECUVARIANTID"].ToString().Trim()));
                        }
                    }
                }
            }

            int fixFuncCount = 0;
            ecuVariant.RefEcuVariantList = refEcuVariantList;

            foreach (EcuFunctionStructs.RefEcuVariant refEcuVariant in refEcuVariantList)
            {
                List<EcuFunctionStructs.EcuFixedFuncStruct> ecuFixedFuncStructList = GetEcuFixedFuncStructList(mDbConnection, refEcuVariant.Id);
                fixFuncCount += ecuFixedFuncStructList.Count;
                refEcuVariant.FixedFuncStructList = ecuFixedFuncStructList;
            }

            List<EcuFunctionStructs.EcuVarFunc> ecuVarFunctionsList = new List<EcuFunctionStructs.EcuVarFunc>();
            foreach (string ecuGroupFunctionId in ecuVariant.GroupFunctionIds)
            {
                // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEcuVariantFunctionByNameAndEcuGroupFunctionId
                string sql = string.Format(@"SELECT ID, VISIBLE, NAME, OBD_RELEVANZ FROM XEP_ECUVARFUNCTIONS WHERE (lower(NAME) = '{0}') AND (ECUGROUPFUNCTIONID = {1})", ecuName.ToLowerInvariant(), ecuGroupFunctionId);
                using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ecuVarFunctionsList.Add(new EcuFunctionStructs.EcuVarFunc(reader["ID"].ToString().Trim(), ecuGroupFunctionId));
                        }
                    }
                }
            }

            foreach (EcuFunctionStructs.EcuVarFunc ecuVarFunc in ecuVarFunctionsList)
            {
                logTextWriter?.WriteLine(ecuVarFunc);
            }

            List<EcuFunctionStructs.EcuFuncStruct> ecuFuncStructList = new List<EcuFunctionStructs.EcuFuncStruct>();
            foreach (EcuFunctionStructs.EcuVarFunc ecuVarFunc in ecuVarFunctionsList)
            {
                // from: DatabaseProvider.SQLiteConnector.dll BMW.Rheingold.DatabaseProvider.SQLiteConnector.DatabaseProviderSQLite.GetEcuFunctionStructureById
                string sql = string.Format(@"SELECT REFFUNCS.ECUFUNCSTRUCTID FUNCSTRUCTID, NODECLASS, " + DatabaseFunctions.SqlTitleItems + ", MULTISELECTION, PARENTID, SORT_ORDER " +
                        "FROM XEP_ECUFUNCSTRUCTURES FUNCS, XEP_REFECUFUNCSTRUCTS REFFUNCS WHERE FUNCS.ID = REFFUNCS.ECUFUNCSTRUCTID AND REFFUNCS.ID = {0}", ecuVarFunc.Id);
                using (SQLiteCommand command = new SQLiteCommand(sql, mDbConnection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string nodeClass = reader["NODECLASS"].ToString();
                            ecuFuncStructList.Add(new EcuFunctionStructs.EcuFuncStruct(reader["FUNCSTRUCTID"].ToString().Trim(),
                                nodeClass,
                                GetNodeClassName(mDbConnection, nodeClass),
                                GetTranslation(reader),
                                reader["MULTISELECTION"].ToString(),
                                reader["PARENTID"].ToString(),
                                reader["SORT_ORDER"].ToString()));
                        }
                    }
                }
            }

            foreach (EcuFunctionStructs.EcuFuncStruct ecuFuncStruct in ecuFuncStructList)
            {
                List<EcuFunctionStructs.EcuFixedFuncStruct> ecuFixedFuncStructList = GetEcuFixedFuncStructList(mDbConnection, ecuFuncStruct.Id);
                fixFuncCount += ecuFixedFuncStructList.Count;
                ecuFuncStruct.FixedFuncStructList = ecuFixedFuncStructList;

                if (ecuFuncStruct.MultiSelect.ConvertToInt() > 0)
                {
                    foreach (EcuFunctionStructs.EcuFixedFuncStruct ecuFixedFuncStruct in ecuFixedFuncStructList)
                    {
                        if (ecuFixedFuncStruct.GetNodeClassType() == EcuFunctionStructs.EcuFixedFuncStruct.NodeClassType.ControlActuator)
                        {
                            outTextWriter.WriteLine("Actuator multi select!");
                        }
                    }
                }
            }

            if (fixFuncCount == 0 && faultCodeCount == 0)
            {
                outTextWriter?.WriteLine("No ECU fix functions or fault codes found");
                return null;
            }

            ecuVariant.EcuFuncStructList = ecuFuncStructList;

            return ecuVariant;
        }

        private static bool CreateZipFile(string inDir, string outFile, string key = null)
        {
            try
            {
                AesCryptoServiceProvider crypto = null;
                FileStream fsOut = null;
                ZipOutputStream zipStream = null;
                try
                {
                    if (!string.IsNullOrEmpty(key))
                    {
                        crypto = new AesCryptoServiceProvider
                        {
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.PKCS7,
                            KeySize = 256
                        };
                        using (SHA256Managed sha256 = new SHA256Managed())
                        {
                            crypto.Key = sha256.ComputeHash(Encoding.ASCII.GetBytes(key));
                        }
                        using (var md5 = MD5.Create())
                        {
                            crypto.IV = md5.ComputeHash(Encoding.ASCII.GetBytes(key));
                        }
                    }

                    fsOut = File.Create(outFile);
                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    if (crypto != null)
                    {
                        CryptoStream crStream = new CryptoStream(fsOut,
                            crypto.CreateEncryptor(), CryptoStreamMode.Write);
                        zipStream = new ZipOutputStream(crStream);
                    }
                    else
                    {
                        zipStream = new ZipOutputStream(fsOut);
                    }


                    zipStream.SetLevel(9); //0-9, 9 being the highest level of compression

                    // This setting will strip the leading part of the folder path in the entries, to
                    // make the entries relative to the starting folder.
                    // To include the full path for each entry up to the drive root, assign folderOffset = 0.
                    int folderOffset = inDir.Length + (inDir.EndsWith("\\") ? 0 : 1);

                    CompressFolder(inDir, zipStream, folderOffset);
                }
                finally
                {
                    if (zipStream != null)
                    {
                        zipStream.IsStreamOwner = true; // Makes the Close also Close the underlying stream
                        zipStream.Close();
                    }
                    fsOut?.Close();
                    crypto?.Dispose();
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private static void CompressFolder(string path, ZipOutputStream zipStream, int folderOffset)
        {
            string[] files = Directory.GetFiles(path);

            foreach (string filename in files)
            {

                FileInfo fi = new FileInfo(filename);

                string entryName = filename.Substring(folderOffset); // Makes the name in zip based on the folder
                entryName = ZipEntry.CleanName(entryName); // Removes drive from name and fixes slash direction
                ZipEntry newEntry = new ZipEntry(entryName);
                newEntry.DateTime = fi.LastWriteTime; // Note the zip format stores 2 second granularity

                // Specifying the AESKeySize triggers AES encryption. Allowable values are 0 (off), 128 or 256.
                // A password on the ZipOutputStream is required if using AES.
                //   newEntry.AESKeySize = 256;

                // To permit the zip to be unpacked by built-in extractor in WinXP and Server2003, WinZip 8, Java, and other older code,
                // you need to do one of the following: Specify UseZip64.Off, or set the Size.
                // If the file may be bigger than 4GB, or you do not need WinXP built-in compatibility, you do not need either,
                // but the zip will be in Zip64 format which not all utilities can understand.
                //   zipStream.UseZip64 = UseZip64.Off;
                newEntry.Size = fi.Length;

                zipStream.PutNextEntry(newEntry);

                // Zip the file in buffered chunks
                // the "using" will close the stream even if an exception occurs
                byte[] buffer = new byte[4096];
                using (FileStream streamReader = File.OpenRead(filename))
                {
                    StreamUtils.Copy(streamReader, zipStream, buffer);
                }
                zipStream.CloseEntry();
            }

            string[] folders = Directory.GetDirectories(path);
            foreach (string folder in folders)
            {
                CompressFolder(folder, zipStream, folderOffset);
            }
        }
    }
}
