﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Indexers
{
    internal class FunctionIndexProvider : IFunctionIndexProvider
    {
        private readonly ITypeLocator _typeLocator;
        private readonly ITriggerBindingProvider _triggerBindingProvider;
        private readonly CompositeBindingProvider _bindingProviderFactory;
        private readonly IJobActivator _activator;
        private readonly IFunctionExecutor _executor;
        private readonly SingletonManager _singletonManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly SharedQueueHandler _sharedQueue;
        private readonly TimeoutAttribute _defaultTimeout;
        private readonly IRetryStrategy _retryStrategy;
        private readonly bool _allowPartialHostStartup;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private IFunctionIndex _index;

        public FunctionIndexProvider(ITypeLocator typeLocator,
            ITriggerBindingProvider triggerBindingProvider,
            CompositeBindingProvider bindingProviderFactory,
            IJobActivator activator,
            IFunctionExecutor executor,
            SingletonManager singletonManager,
            ILoggerFactory loggerFactory,
            SharedQueueHandler sharedQueue,
            IOptions<JobHostFunctionTimeoutOptions> timeoutOptions,
            IOptions<JobHostOptions> hostOptions,
            IConfiguration configuration,
            IServiceScopeFactory serviceScopeFactory,
            IOptions<JobHostLinearBackoffRetryOptions> fixedDelayRetryOptions,
            IOptions<JobHostExponentialBackoffRetryOptions> exponentialRetryOptions)
        {
            _typeLocator = typeLocator ?? throw new ArgumentNullException(nameof(typeLocator));
            _triggerBindingProvider = triggerBindingProvider ?? throw new ArgumentNullException(nameof(triggerBindingProvider));
            _bindingProviderFactory = bindingProviderFactory ?? throw new ArgumentNullException(nameof(bindingProviderFactory));
            if (fixedDelayRetryOptions.Value.MaxRetryCount != 0 && exponentialRetryOptions.Value.MaxRetryCount != 0)
            {
                throw new InvalidOperationException($"{nameof(JobHostLinearBackoffRetryOptions)} and {nameof(JobHostExponentialBackoffRetryOptions)} specified. Provide only one of them.");
            }
            _activator = activator ?? throw new ArgumentNullException(nameof(activator));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _singletonManager = singletonManager ?? throw new ArgumentNullException(nameof(singletonManager));
            _sharedQueue = sharedQueue ?? throw new ArgumentNullException(nameof(sharedQueue));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _loggerFactory = loggerFactory;
            _defaultTimeout = timeoutOptions.Value.ToAttribute();
            _allowPartialHostStartup = hostOptions.Value.AllowPartialHostStartup;
            _retryStrategy = fixedDelayRetryOptions.Value.MaxRetryCount != 0 ? (IRetryStrategy) new FixedDelayRetryStrategy(fixedDelayRetryOptions.Value.MaxRetryCount, fixedDelayRetryOptions.Value.Delay) : new ExponentialBackoffRetryStrategy(exponentialRetryOptions.Value.MaxRetryCount, exponentialRetryOptions.Value.MinimumDelay, exponentialRetryOptions.Value.MaximumDelay);
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<IFunctionIndex> GetAsync(CancellationToken cancellationToken)
        {
            if (_index == null)
            {
                _index = await CreateAsync(cancellationToken);
            }

            return _index;
        }

        private async Task<IFunctionIndex> CreateAsync(CancellationToken cancellationToken)
        {
            FunctionIndex index = new FunctionIndex();
            IBindingProvider bindingProvider = _bindingProviderFactory;
            FunctionIndexer indexer = new FunctionIndexer(_triggerBindingProvider, 
                bindingProvider, 
                _activator, 
                _executor, 
                _singletonManager, 
                _loggerFactory, 
                _configuration, 
                _serviceScopeFactory, 
                null, 
                _sharedQueue, 
                _defaultTimeout, 
                _allowPartialHostStartup,
                _retryStrategy); 

            IReadOnlyList<Type> types = _typeLocator.GetTypes();

            foreach (Type type in types)
            {
                await indexer.IndexTypeAsync(type, index, cancellationToken);
            }

            return index;
        }
    }
}