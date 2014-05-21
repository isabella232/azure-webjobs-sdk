﻿using System;
using System.Globalization;
using Microsoft.Azure.Jobs.Host.Protocols;

namespace Microsoft.Azure.Jobs
{
    internal class TableEntityParameterStaticBinding : ParameterStaticBinding
    {
        private string _tableName;
        private string _partitionKey;
        private string _rowKey;

        public string TableName
        {
            get
            {
                return _tableName;
            }
            set
            {
                if (!RouteParser.HasParameterNames(value))
                {
                    TableClient.ValidateAzureTableName(value);
                }

                _tableName = value;
            }
        }

        public string PartitionKey
        {
            get
            {
                return _partitionKey;
            }
            set
            {
                if (!RouteParser.HasParameterNames(value))
                {
                    TableClient.ValidateAzureTableKeyValue(value);
                }

                _partitionKey = value;
            }
        }

        public string RowKey
        {
            get
            {
                return _rowKey;
            }
            set
            {
                if (!RouteParser.HasParameterNames(value))
                {
                    TableClient.ValidateAzureTableKeyValue(value);
                }

                _rowKey = value;
            }
        }

        public override ParameterRuntimeBinding Bind(IRuntimeBindingInputs inputs)
        {
            CloudTableEntityDescriptor entity = CloudTableEntityDescriptor.ApplyNames(TableName, PartitionKey, RowKey, inputs.NameParameters);
            return BindCore(Name, entity, inputs);
        }

        public override ParameterRuntimeBinding BindFromInvokeString(IRuntimeBindingInputs inputs, string invokeString)
        {
            CloudTableEntityDescriptor entity = CloudTableEntityDescriptor.Parse(invokeString);
            return BindCore(Name, entity, inputs);
        }

        private static ParameterRuntimeBinding BindCore(string name, CloudTableEntityDescriptor entity, IRuntimeBindingInputs inputs)
        {
            entity.Validate();
            entity.AccountConnectionString = inputs.AccountConnectionString;

            return new TableEntityParameterRuntimeBinding { Name = name, Entity = entity };
        }

        public override ParameterDescriptor ToParameterDescriptor()
        {
            return new TableEntityParameterDescriptor
            {
                TableName = TableName,
                PartitionKey = PartitionKey,
                RowKey = RowKey
            };
        }
    }
}