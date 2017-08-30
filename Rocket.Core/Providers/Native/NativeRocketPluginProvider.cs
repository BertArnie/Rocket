﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Rocket.API.Commands;
using Rocket.API.Providers;
using Rocket.Core.Commands;
using Rocket.API.Plugins;
using Rocket.API.Logging;

namespace Rocket.Core.Providers.Plugin.Native
{
   
    public sealed class NativeRocketPluginProvider : ProviderBase, IPluginProvider
    {
        public static readonly string PluginDirectory = "Plugins/{0}/";
        public static readonly string PluginTranslationFileTemplate = "{0}.{1}.translation.xml";
        public static readonly string PluginConfigurationFileTemplate = "{0}.configuration.xml";
        public static NativeRocketPluginProvider Instance { get; private set; }
        private static List<Assembly> pluginAssemblies = new List<Assembly>();
        private static List<NativeRocketPlugin> plugins = new List<NativeRocketPlugin>();
        private Dictionary<string, string> libraries = new Dictionary<string, string>();

        public ReadOnlyCollection<IPlugin> GetPlugins()
        {
            return plugins.Select(g => g.GetComponent<NativeRocketPlugin>()).Where(p => p != null).Select(p => (IPlugin)p).ToList().AsReadOnly();
        }

        public IPlugin GetPlugin(string name)
        {
            return plugins.Select(g => g.GetComponent<NativeRocketPlugin>()).FirstOrDefault(p => p != null && p.GetType().Assembly.GetName().Name == name);
        }

        public string GetPluginDirectory(string name)
        {
            return Path.Combine(PluginsDirectory, name) + "/";
        }

        public string PluginsDirectory { get; private set; }

        public ReadOnlyCollection<Type> Providers => new List<Type>().AsReadOnly();

        public List<ICommand> GetCommandTypesFromAssembly(Assembly assembly, Type plugin)
        {
            List<ICommand> commands = new List<ICommand>();
            List<Type> commandTypes = GetTypesFromInterface(assembly, "IRocketCommand");
            foreach (Type commandType in commandTypes)
            {
                if (commandType.GetConstructor(Type.EmptyTypes) != null)
                {
                    ICommand command = (ICommand)Activator.CreateInstance(commandType);
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

                        ICommand command = new AttributeCommand(this, commandAttribute.Name, commandAttribute.Help, commandAttribute.Syntax, commandAttribute.AllowedCaller, Permissions, Aliases, method);
                        commands.Add(command);
                    }
                }
            }
            return commands;
        }

        public void LoadPlugins()
        {
            PluginsDirectory = "Plugins";
            
            libraries = GetAssembliesFromDirectory("Libraries");
            pluginAssemblies = LoadAssembliesFromDirectory(PluginsDirectory);

            foreach (Assembly pluginAssembly in pluginAssemblies)
            {
                List<Type> pluginImplemenations = GetTypesFromInterface(pluginAssembly, "IPlugin");

                foreach (Type pluginType in pluginImplemenations)
                {
                    //gameObject.TryAddComponent(pluginType);
                    CommandProvider.AddRange(GetCommandTypesFromAssembly(pluginAssembly, pluginType));
                }
            }
        }

        private void unloadPlugins()
        {
            for (int i = plugins.Count; i > 0; i--)
            {
                //Destroy(plugins[i - 1]);
            }
            plugins.Clear();
        }

        private static Dictionary<string, string> GetAssembliesFromDirectory(string directory, string searchPattern = "*.dll")
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
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

        public void AddCommands(IEnumerable<ICommand> commands)
        {
            CommandProvider.AddRange(commands.AsEnumerable());
        }

        private List<Assembly> LoadAssembliesFromDirectory(string directory, string extension = "*.dll")
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            List<Assembly> assemblies = new List<Assembly>();
            IEnumerable<FileInfo> pluginsLibraries = new DirectoryInfo(directory).GetFiles(extension, SearchOption.TopDirectoryOnly);

            foreach (FileInfo library in pluginsLibraries)
            {
                try
                {
                    Assembly assembly = Assembly.Load(File.ReadAllBytes(library.FullName));

                    if (GetTypesFromInterface(assembly, "IPlugin").Count == 1)
                    {
                        assemblies.Add(assembly);
                    }
                    else
                    {
                        Logging.LogMessage(LogLevel.ERROR, "Invalid or outdated plugin assembly: " + assembly.GetName().Name, ConsoleColor.DarkRed);
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log(LogLevel.ERROR, "Could not load plugin assembly: " + library.Name, ex);
                }
            }
            return assemblies;
        }

        public void Load(bool isReload = false)
        {
            try
            {
                Instance = this;
                CommandProvider = new CommandList(this);
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
                Logging.Log(LogLevel.FATAL, ex);
            }
        }

        public static List<Type> GetTypesFromInterface(Assembly assembly, string interfaceName)
        {
            List<Type> allTypes = new List<Type>();
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }
            foreach (Type type in types.Where(t => t != null))
            {
                if (type.GetInterface(interfaceName) != null)
                {
                    allTypes.Add(type);
                }
            }
            return allTypes;
        }

        public CommandList CommandProvider { get; set; }

        public void Unload(bool isReload)
        {
            unloadPlugins();
        }

        public ReadOnlyCollection<IPlugin> Plugins => GetPlugins();
        public ReadOnlyCollection<Type> LoadProviders()
        {
            return Providers;
        }

        ILoggingProvider Logging;

        protected override void OnLoad(ProviderManager providerManager)
        {
            Logging = providerManager.GetProvider<ILoggingProvider>();
        }

        protected override void OnUnload()
        {
            throw new NotImplementedException();
        }
    }
}