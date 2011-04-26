﻿using System;
using System.Diagnostics;
using TechTalk.SpecFlow.Generator;
using TechTalk.SpecFlow.Generator.Interfaces;
using TechTalk.SpecFlow.IdeIntegration.Tracing;

namespace TechTalk.SpecFlow.IdeIntegration.Generator
{
    public class GeneratorInfo
    {
        public Version GeneratorAssemblyVersion { get; set; }
        public string GeneratorFolder { get; set; }
    }

    public abstract class RemoteGeneratorServices : GeneratorServices
    {
        private RemoteAppDomainTestGeneratorFactory remoteAppDomainTestGeneratorFactory;

        protected RemoteGeneratorServices(ITestGeneratorFactory testGeneratorFactory, IIdeTracer tracer, bool enableSettingsCache) : base(testGeneratorFactory, tracer, enableSettingsCache)
        {
        }

        protected abstract GeneratorInfo GetGeneratorInfo();

        protected Version GetCurrentGeneratorAssemblyVersion()
        {
            return typeof(TestGeneratorFactory).Assembly.GetName().Version;
        }

        protected override ITestGeneratorFactory GetTestGeneratorFactoryForCreate()
        {
            // if we already have a generator factory -> use it!
            if (remoteAppDomainTestGeneratorFactory != null)
                return remoteAppDomainTestGeneratorFactory;

            GeneratorInfo generatorInfo = GetGeneratorInfo();
            if (generatorInfo == null || generatorInfo.GeneratorAssemblyVersion == null || generatorInfo.GeneratorFolder == null)
            {
                // we don't know about the generator -> call the "current" directly
                tracer.Trace("Unable to detect generator location: the generator bound to the IDE is used", "RemoteGeneratorServices");
                return base.GetTestGeneratorFactoryForCreate();
            }

            if (generatorInfo.GeneratorAssemblyVersion < new Version(1, 6))
            {
                // old generator version -> call the "current" directly
                tracer.Trace(string.Format("The project's generator ({0}) is older than v1.6: the generator bound to the IDE is used", generatorInfo.GeneratorAssemblyVersion), "RemoteGeneratorServices");
                return base.GetTestGeneratorFactoryForCreate();
            }

            if (generatorInfo.GeneratorAssemblyVersion == GetCurrentGeneratorAssemblyVersion())
            {
                // uses the "current" generator -> call it directly
                tracer.Trace("The generator of the project is the same as the generator bound to the IDE: using it from the IDE", "RemoteGeneratorServices");
                return base.GetTestGeneratorFactoryForCreate();
            }

            try
            {
                tracer.Trace(string.Format("Creating remote wrapper for the project's generator ({0} at {1})", generatorInfo.GeneratorAssemblyVersion, generatorInfo.GeneratorFolder), "RemoteGeneratorServices");
                remoteAppDomainTestGeneratorFactory = new RemoteAppDomainTestGeneratorFactory(tracer, generatorInfo.GeneratorFolder);
                remoteAppDomainTestGeneratorFactory.Initialize();
                return remoteAppDomainTestGeneratorFactory;
            }
            catch(Exception exception)
            {
                tracer.Trace(exception.ToString(), "RemoteGeneratorServices");
                // there was an error -> call the "current" directly (plus cleanup)
                Cleanup();
                return base.GetTestGeneratorFactoryForCreate();
            }
        }

        public override void InvalidateSettings()
        {
            Cleanup();

            base.InvalidateSettings();
        }

        public override void Dispose()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (remoteAppDomainTestGeneratorFactory != null)
            {
                remoteAppDomainTestGeneratorFactory.Dispose();
                remoteAppDomainTestGeneratorFactory = null;
            }
        }
    }
}