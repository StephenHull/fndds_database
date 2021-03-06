﻿namespace FpedLoader
{
    using Loader;
    using Loader.Tables;
    using log4net;
    using log4net.Config;
    using Model;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data.OleDb;
    using System.Linq;
    using System.Threading.Tasks;
    using Utility;

    /// <summary>
    /// This class is a utility for loading FPED data from USDA into a database.
    /// </summary>
    public class FpedLoader
    {
        /// <summary>
        /// The logger class.
        /// </summary>
        private static readonly ILog _logger = LogManager.GetLogger(typeof(FpedLoader));

        /// <summary>
        /// True if the logger is debug endabled; otherwise, false.
        /// </summary>
        private bool _isDebugEnabled = false;

        /// <summary>
        /// Gets or sets the connection string for the destination database.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Constructs a new FPED loader.
        /// </summary>
        public FpedLoader()
        {
            // Configure the logger
            XmlConfigurator.Configure();

            _isDebugEnabled = _logger.IsDebugEnabled;

            // Get the connection string
            var settings = ConfigurationManager.ConnectionStrings["Fped"];
            if (settings != null)
            {
                ConnectionString = settings.ConnectionString;
            }
        }

        /// <summary>
        /// Imports data from a source database.
        /// </summary>
        /// <param name="fnddsVersion">The FNDDS version.</param>
        /// <param name="connString">The connection string for the source database.</param>
        /// <returns>Returns true if the method completes successfully.</returns>
        public async Task<bool> ImportDataAsync(FnddsVersion fnddsVersion, string connString)
        {
            using (var context = new FpedContext())
            {
                var version = context.FnddsVersion.SingleOrDefault(x => x.Id == fnddsVersion.Id);
                if (version == null)
                {
                    Environment.Exit(0);
                }

                var canEquivalents = (version.Id > 2 && version.Id < 128);
                if (canEquivalents)
                {
                    if (version.Id == 4)
                    {
                        EquivalentLoader.SourceTableName = "FPED_0506";
                        ModEquivalentLoader.SourceTableName = "FPED_0506";
                    }
                    else if (version.Id == 8)
                    {
                        EquivalentLoader.SourceTableName = "FPED_0708";
                        ModEquivalentLoader.SourceTableName = "FPED_0708";
                    }
                    else if (version.Id == 16)
                    {
                        EquivalentLoader.SourceTableName = "FPED_0910";
                        ModEquivalentLoader.SourceTableName = "FPED_0910";
                    }
                    else if (version.Id == 32)
                    {
                        EquivalentLoader.SourceTableName = "FPED_1112";
                        ModEquivalentLoader.SourceTableName = "FPED_1112";
                    }
                    else if (version.Id == 64)
                    {
                        EquivalentLoader.SourceTableName = "FPED_1314";
                        ModEquivalentLoader.SourceTableName = "FPED_1314";
                    }

                    using (var connection = new OleDbConnection(connString))
                    {
                        await connection.OpenAsync();

                        var loaders = new List<DataLoader> { new EquivalentLoader(version, connection, context) };

                        if (version.Id < 64)
                        {
                            loaders.Add(new ModEquivalentLoader(version, connection, context));
                        }

                        foreach (var loader in loaders)
                        {
                            var recordsLoaded = await loader.LoadAsync();

                            if (_isDebugEnabled)
                            {
                                _logger.DebugFormat("Table: {0}, Records: {1}", loader.TableName, recordsLoaded);
                            }
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// The main method. This method simply calls MainAsync.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <remarks>
        /// Arguments:
        ///     fnddsVersion {Integer} the FNDDS version
        ///         1   = FNDDS 1.0 (2001-2002)
        ///         2   = FNDDS 2.0 (2003-2004)
        ///         4   = FNDDS 3.0 (2005-2006)
        ///         8   = FNDDS 4.1 (2007-2008)
        ///         16  = FNDDS 5.0 (2009-2010)
        ///         32  = FNDDS 2011-2012
        ///         64  = FNDDS 2013-2014
        ///         128 = FNDDS 2015-2016
        ///     connString {String} the FPED Access database OLEDB connection string
        /// </remarks>
        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        /// <summary>
        /// The async main method. This method is where the work is done.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        public static async Task MainAsync(string[] args)
        {
            if (args.Length < 2)
            {
                _logger.Fatal("Missing connand-line arguments.");

                Environment.Exit(0);
            }

            _logger.DebugFormat("FNDDS Version ID: {0}", args[0]);
            _logger.DebugFormat("Local Connection String: {0}", args[1]);

            var loader = new FpedLoader();

            var id = -1;
            var connString = string.Empty;

            try
            {
                id = Convert.ToInt32(args[0]);
                connString = args[1].ToString();
            }
            catch (Exception e)
            {
                _logger.Fatal("An error occurred parsing the connand-line arguments.", e);
            }

            var version = VersionUtility.GetVersionFromId(id);
            if (version == null)
            {
                _logger.Fatal("Invalid FNDDS version.");

                Environment.Exit(0);
            }

            await loader.ImportDataAsync(version, connString);
        }
    }
}