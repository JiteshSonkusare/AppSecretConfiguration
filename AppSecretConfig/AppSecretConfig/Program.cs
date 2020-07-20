using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppSecretConfig
{
    public class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args == null) 
                throw new ArgumentNullException(nameof(args));

            args = new[] { "ManageConfiguration" };

            var commandLineArgs = Parser.Default.ParseArguments<
                ManageConfiguration,
                ManageTestConfiguration>(args);
            await CreateHostBuilder(commandLineArgs);
            return 0;
        }

        private static async Task CreateHostBuilder(ParserResult<object> commandLineArgs) =>
            await Host.CreateDefaultBuilder(null)
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;

                    services.AddHostedService<ConfigurationService>();
                    services.AddSingleton(commandLineArgs);
                })
                .RunConsoleAsync();
    }

    public class ConfigurationService : IHostedService, IDisposable
    {
        protected ILogger<ConfigurationService> Logger { get; set; }
        protected IConfiguration Configuration { get; }
        protected ParserResult<object> CommandLineArgs { get; }

        public ConfigurationService(IConfiguration configuration, ILogger<ConfigurationService> logger, ParserResult<object> commandLineArgs) =>
            (Configuration, Logger, CommandLineArgs) = (configuration, logger, commandLineArgs);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            CommandLineArgs
                .WithParsed<ManageConfiguration>(manageInvoice =>
                    manageInvoice.Execute(Configuration, Logger).Wait(cancellationToken));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        { }
    }

    public abstract class Option
    {
        protected ILogger Logger { get; set; }

        protected IntegrationConfiguration Configuration;
        public async Task Execute(IConfiguration configuration, ILogger logger)
        {
            Configuration = configuration.GetSection("IntegrationConfiguration").Get<IntegrationConfiguration>();
            await Execute();
        }

        public abstract Task Execute();
    }

    [Verb("ManageConfiguration", HelpText = "Sync ManageConfiguration")]
    public class ManageConfiguration : Option
    {
        public override async Task Execute()
        {
            var contractingConfiguration = Configuration.GetConfiguration<ContractingConfiguration>("ContractingConfiguration");

            Console.WriteLine("Manage Configuration Called!");
            await Task.CompletedTask;
        }
    }

    [Verb("ManageTestConfiguration", HelpText = "Sync ManageTestConfiguration")]
    public class ManageTestConfiguration : Option
    {
        public override async Task Execute()
        {
            Console.WriteLine("Manage Test Configuration Called!");
            await Task.CompletedTask;
        }
    }

    public class IntegrationConfiguration : DataConfiguration
    {
        protected Dictionary<string, object>[] ProviderConfiguration
        {
            get => (Dictionary<string, object>[])GetProperty();
            set => SetProperty(value);
        }

        public TProviderConfig GetConfiguration<TProviderConfig>(string provider) where TProviderConfig : class
        {
            var providerConfig = ProviderConfiguration.Where(x => x.ContainsKey(provider));

            if (providerConfig == null)
                throw new Exception($"Provider {provider} not found in the configuration");

            var providerInstance = Activator.CreateInstance(typeof(TProviderConfig), new object[] { providerConfig }) as TProviderConfig;
            return providerInstance;
        }
    }

    public abstract class DataConfiguration
    {
        protected internal Dictionary<string, object> PropertyBag { get; set; } = new Dictionary<string, object>();

        protected object GetProperty([CallerMemberName] string propertyName = "")
            => PropertyBag.TryGetValue(propertyName, out object value) ? value : null;

        protected object SetProperty(object value, [CallerMemberName] string propertyName = "")
            => PropertyBag[propertyName] = value;
    }


    public abstract class SecretConfiguration : DataConfiguration
    {
        protected SecretConfiguration(Dictionary<string, object> providerConfig) =>
            PropertyBag = providerConfig;

        public string GetString([CallerMemberName] string propertyName = "") =>
            (string)GetProperty(propertyName);

        protected string SetString(string value, [CallerMemberName] string propertyName = "") =>
            (string)SetProperty(value, propertyName);
    }


    public class ContractingConfiguration : SecretConfiguration
    {
        public ContractingConfiguration(Dictionary<string, object> providerConfig) : base(providerConfig)
        { }

        internal ContractingConfiguration(string username, string password) : base(
            new Dictionary<string, object>
            {
                [nameof(Username)] = username,
                [nameof(Password)] = password
            })
        { }

        public string Username => GetString();
        public string Password => GetString();
    }
}
