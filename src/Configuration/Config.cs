﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nvelope;
using System.Configuration;

namespace Nvelope.Configuration
{
    public static class Config
    {
        // TODO: Remove this
        // In order to do so, you need to add a DeploymentLocation environment variable to production
        private const string PRODUCTION_MACHINE_CONFIG_SETTING_NAME = "IsTwuProduction";

        public const string DEPLOYMENT_ENV_VAR = "DeploymentLocation";

        public static bool HasSetting(string name)
        {
            return Setting(name) != null;
        }

        public static bool HasConnectionString(string name)
        {
            return ConnectionString(name) != null;
        }

        public static DeploymentLocation Location
        {
            get
            {
                return _getLocation();
            }
        }
        
        private static DeploymentLocation _getLocation()
        {            
            // Check the machine.config for the setting
            // that indicates whether this machine is a production machine

            // TODO: Deprecated. This setting is set to true on the machine.config on the production machines
            // Instead, we should just use the DEPLOYMENT_ENV_VAR environment variable to set it instead
            var isProduction = ConfigurationManager.AppSettings[PRODUCTION_MACHINE_CONFIG_SETTING_NAME].ConvertTo<bool?>() ?? false;                

            if (isProduction)
                return DeploymentLocation.Live;

            var loc = Environment.GetEnvironmentVariable(DEPLOYMENT_ENV_VAR);
            if (loc.CanConvertTo<DeploymentLocation>())
                return loc.ConvertTo<DeploymentLocation>();

            // Hack here to see if we're running on cassini
            if (System.Environment.CommandLine.Contains("WebDev.WebServer"))
                return DeploymentLocation.Cassini;

            return DeploymentLocation.Local;
        }

        /// <summary>
        /// If the config file has a setting, return it, else retun the default_value
        /// </summary>
        public static string Setting(string name, string default_value = null, bool throw_if_missing = false)
        {
            var possibleNames = _getLocationizedNames(Location, name);

            var res = possibleNames.Select(n => ConfigurationManager.AppSettings[n])
                .Where(s => s != null)
                .FirstOr(default_value);

            if (throw_if_missing && res == null)
                throw new ConfigurationException("The setting '" + name + "' was not found in the config file!");

            return res;
        }

        public static string ConnectionString(string name, string default_value = null)
        {
            var possibleNames = _getLocationizedNames(Location, name);

            var res = possibleNames.Select(n => ConfigurationManager.ConnectionStrings[n])
                .Where(s => s != null)
                .FirstOr(null);

            if (res != null)
                return res.ConnectionString;
            else
                return default_value;
        }

        /// <summary>
        /// Get a list of the possible names (ordered by priority) that the setting
        /// could have in the config file, depending on the supplied location.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> _getLocationizedNames(DeploymentLocation loc, string name)
        {
            if (loc == DeploymentLocation.Live)
                yield return name + "-live";
            if (loc == DeploymentLocation.Dev)
                yield return name + "-dev";
            if (loc == DeploymentLocation.Local) {
                yield return name + "-local";
                yield return name + "-dev";
            }
            if (loc == DeploymentLocation.Cassini)
            {
                yield return name + "-cassini";
                yield return name + "-local";
                yield return name + "-dev";
            }
            yield return name;
        }
    }
}
