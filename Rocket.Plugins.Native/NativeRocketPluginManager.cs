﻿using Rocket.API.Plugins;
using Rocket.Core.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Rocket.API.Commands;
using Logger = Rocket.API.Logging.Logger;
using Rocket.API.Extensions;

namespace Rocket.Plugins.Native
{
    public sealed class NativeRocketPluginManager : MonoBehaviour, IRocketPluginManager
    {
        public static NativeRocketPluginManager Instance { get; private set; }
        private static List<Assembly> pluginAssemblies;
        private static List<NativeRocketPlugin> plugins = new List<NativeRocketPlugin>();
        private Dictionary<string, string> libraries = new Dictionary<string, string>();

        public InitialiseDelegate Initialise { get; set; }

        public RocketCommandList Commands { get; private set; }

        public List<IRocketPlugin> GetPlugins()
        {
            return plugins.Select(g => g.GetComponent<NativeRocketPlugin>()).Where(p => p != null).Select(p => (IRocketPlugin)p).ToList();
        }

        public IRocketPlugin GetPlugin(string name)
        {
            return plugins.Select(g => g.GetComponent<NativeRocketPlugin>()).Where(p => p != null && p.GetType().Assembly.GetName().Name == name).FirstOrDefault();
        }

        public string GetPluginDirectory(string name)
        {
            return Path.Combine(PluginsDirectory, name)+"/";
        }

        public string PluginsDirectory { get; private set; }
        string librariesDirectory;
        string languageCode = "en";
        public void Load(string pluginDirectory, string languageCode, string librariesDirectory)
        {
            PluginsDirectory = pluginDirectory;
            this.librariesDirectory = librariesDirectory;
            this.languageCode = languageCode;
            loadPlugins();
        }

        public void Load(string pluginDirectory, string languageCode)
        {
            throw new NotImplementedException();
        }

        private void Awake()
        {
            try
            {
                Instance = this;
                Commands = new RocketCommandList(this);
                AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs args)
                {
                    string file;
                    if (libraries.TryGetValue(args.Name, out file))
                    {
                        return Assembly.Load(File.ReadAllBytes(file));
                    }
                    return null;
                };

            }
            catch (Exception ex)
            {
                Logger.Fatal(ex);
            }
        }

        public List<IRocketCommand> GetCommandTypesFromAssembly(Assembly assembly, Type plugin)
        {
            List<IRocketCommand> commands = new List<IRocketCommand>();
            List<Type> commandTypes = assembly.GetTypesFromInterface("IRocketCommand");
            foreach (Type commandType in commandTypes)
            {
                if (commandType.GetConstructor(Type.EmptyTypes) != null)
                {
                    IRocketCommand command = (IRocketCommand)Activator.CreateInstance(commandType);
                    commands.Add(command);
                }
            }

            if (plugin != null)
            {
                MethodInfo[] methodInfos = plugin.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                foreach (MethodInfo method in methodInfos)
                {
                    RocketCommandAttribute commandAttribute = (RocketCommandAttribute)Attribute.GetCustomAttribute(method, typeof(RocketCommandAttribute));
                    RocketCommandAliasAttribute[] commandAliasAttributes = (RocketCommandAliasAttribute[])Attribute.GetCustomAttributes(method, typeof(RocketCommandAliasAttribute));
                    RocketCommandPermissionAttribute[] commandPermissionAttributes = (RocketCommandPermissionAttribute[])Attribute.GetCustomAttributes(method, typeof(RocketCommandPermissionAttribute));

                    if (commandAttribute != null)
                    {
                        List<string> Permissions = new List<string>();
                        List<string> Aliases = new List<string>();

                        if (commandAliasAttributes != null)
                        {
                            foreach (RocketCommandAliasAttribute commandAliasAttribute in commandAliasAttributes)
                            {
                                Aliases.Add(commandAliasAttribute.Name);
                            }
                        }

                        if (commandPermissionAttributes != null)
                        {
                            foreach (RocketCommandPermissionAttribute commandPermissionAttribute in commandPermissionAttributes)
                            {
                                Aliases.Add(commandPermissionAttribute.Name);
                            }
                        }

                        IRocketCommand command = new RocketAttributeCommand(this,commandAttribute.Name, commandAttribute.Help, commandAttribute.Syntax, commandAttribute.AllowedCaller, Permissions, Aliases, method);
                        commands.Add(command);
                    }
                }
            }
            return commands;
        }

        private void loadPlugins()
        {
            libraries = GetAssembliesFromDirectory(librariesDirectory);
            pluginAssemblies = LoadAssembliesFromDirectory(PluginsDirectory);

            foreach (Assembly pluginAssembly in pluginAssemblies)
            {
                List<Type> pluginImplemenations = pluginAssembly.GetTypesFromInterface("IRocketPlugin");

                foreach (Type pluginType in pluginImplemenations)
                {
                    gameObject.TryAddComponent(pluginType);
                    Commands.AddRange(GetCommandTypesFromAssembly(pluginAssembly, pluginType));
                }
            }
        }

        private void unloadPlugins()
        {
            for (int i = plugins.Count; i > 0; i--)
            {
                Destroy(plugins[i - 1]);
            }
            plugins.Clear();
        }

        public void Unload()
        {
            unloadPlugins();
        }

        public void Reload()
        {
            unloadPlugins();
            loadPlugins();
        }

        private static Dictionary<string, string> GetAssembliesFromDirectory(string directory, string searchPattern = "*.dll")
        {
            Dictionary<string, string> l = new Dictionary<string, string>();
            IEnumerable<FileInfo> libraries = new DirectoryInfo(directory).GetFiles(searchPattern, SearchOption.AllDirectories);
            foreach (FileInfo library in libraries)
            {
                try
                {
                    AssemblyName name = AssemblyName.GetAssemblyName(library.FullName);
                    l.Add(name.FullName, library.FullName);
                }
                catch { }
            }
            return l;
        }

        public void AddCommands(IEnumerable<IRocketCommand> commands)
        {
            Commands.AddRange(commands.AsEnumerable());
        }

        private static List<Assembly> LoadAssembliesFromDirectory(string directory, string extension = "*.dll")
        {
            List<Assembly> assemblies = new List<Assembly>();
            IEnumerable<FileInfo> pluginsLibraries = new DirectoryInfo(directory).GetFiles(extension, SearchOption.TopDirectoryOnly);

            foreach (FileInfo library in pluginsLibraries)
            {
                try
                {
                    Assembly assembly = Assembly.Load(File.ReadAllBytes(library.FullName));

                    if (assembly.GetTypesFromInterface("IRocketPlugin").Count == 1)
                    {
                        assemblies.Add(assembly);
                    }
                    else
                    {
                        Logger.Error("Invalid or outdated plugin assembly: " + assembly.GetName().Name);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Could not load plugin assembly: " + library.Name, ex);
                }
            }
            return assemblies;
        }
    }
}