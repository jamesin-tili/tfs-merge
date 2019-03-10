using Caliburn.Micro;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System;
using System.Linq;
using TFSMergingTool.Shell;
using System.Windows;
using TFSMergingTool.Settings;
using TFSMergingTool.OutputWindow;
using System.Collections.Generic;
using TFSMergingTool.ConnectionSetup;
using System.Windows.Input;
using System.Windows.Threading;

namespace TFSMergingTool.Resources
{
    class MefBootstrapper : BootstrapperBase
    {
        private CompositionContainer container;

        public MefBootstrapper()
        {
            Initialize();
        }

        protected override void Configure()
        {
            container = new CompositionContainer(new AggregateCatalog(AssemblySource.Instance.Select(x => new AssemblyCatalog(x)).OfType<ComposablePartCatalog>()));

            CompositionBatch batch = new CompositionBatch();

            batch.AddExportedValue<IWindowManager>(new WindowManager());
            batch.AddExportedValue<IEventAggregator>(new EventAggregator());
            batch.AddExportedValue<UserSettings>(new UserSettings());
            batch.AddExportedValue(container);

            container.Compose(batch);

            MessageBinder.SpecialValues.Add("$pressedkey", (context) =>
            {
                // NOTE: IMPORTANT - you MUST add the dictionary key as lowercase as CM
                // does a ToLower on the param string you add in the action message, in fact ideally
                // all your param messages should be lowercase just in case. I don't really like this
                // behaviour but that's how it is!
                var keyArgs = context.EventArgs as KeyEventArgs;

                if (keyArgs != null)
                    return keyArgs.Key;

                return null;
            });
        }

        protected override object GetInstance(Type serviceType, string key)
        {
            string contract = string.IsNullOrEmpty(key) ? AttributedModelServices.GetContractName(serviceType) : key;
            var exports = container.GetExportedValues<object>(contract);

            if (exports.Any())
                return exports.First();

            throw new Exception(string.Format("Could not locate any instances of contract {0}.", contract));
        }

        protected override IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return container.GetExportedValues<object>(AttributedModelServices.GetContractName(serviceType));
        }

        protected override void BuildUp(object instance)
        {
            container.SatisfyImportsOnce(instance);
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            DisplayRootViewFor<IShell>();
        }

        protected override void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            base.OnUnhandledException(sender, e);

            if (e.Exception is Microsoft.TeamFoundation.TeamFoundationServiceUnavailableException)
                MessageBox.Show("TFS Server connection is acting up (TeamFoundationServiceUnavailableException)... VS restart is recommended.");
        }

    }
}
