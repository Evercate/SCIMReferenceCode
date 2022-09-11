//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.SCIM
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;

    internal class JsonSerializer : IJsonSerializable
    {
        private static readonly Lazy<DataContractJsonSerializerSettings> SerializerSettings =
            new Lazy<DataContractJsonSerializerSettings>(
                () =>
                    new DataContractJsonSerializerSettings()
                    {
                        EmitTypeInformation = EmitTypeInformation.Never
                    });

        private readonly object dataContractValue;

        public JsonSerializer(object dataContract)
        {
            this.dataContractValue = dataContract ??
                throw new ArgumentNullException(nameof(dataContract));
        }

        public string Serialize()
        {
            IDictionary<string, object> json = this.ToJson();
            string result = JsonFactory.Instance.Create(json, true);
            return result;
        }

        public Dictionary<string, object> ToJson()
        {
            Type type = this.dataContractValue.GetType();
            DataContractJsonSerializer serializer =
                new DataContractJsonSerializer(type, JsonSerializer.SerializerSettings.Value);

            //Fix for a strange bug where serializer.WriteObject would cause stack overflow exception when serializing the operation.Value when it's a JArray (add member to group)
            //This is a hacky workaround only
            var stringConvertedArraysCount = 0;
            if (this.dataContractValue is PatchRequest2)
            {
                var patchRequest = ((PatchRequest2)this.dataContractValue);
                foreach (var operation in patchRequest.Operations)
                {
                    //The Get will serialize the value
                    var serializedValue = operation?.Value?.Trim();

                    //Extremely basic check if it's an array
                    if (serializedValue[0] == '[' && serializedValue[serializedValue.Length - 1] == ']')
                    {
                        //When we set the value it meerely sets it. We basically change backing field type from JArray to string (it's defined as object)
                        operation.Value = serializedValue;

                        stringConvertedArraysCount++;
                    }

                }
            }


            string json;
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream();
                serializer.WriteObject(stream, this.dataContractValue);
                stream.Position = 0;
                StreamReader streamReader = null;
                try
                {
                    streamReader = new StreamReader(stream);
                    stream = null;
                    json = streamReader.ReadToEnd();
                }
                finally
                {
                    if (streamReader != null)
                    {
                        streamReader.Close();
                        streamReader = null;
                    }
                }
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                    stream = null;
                }
            }

            Dictionary<string, object> result = JsonFactory.Instance.Create(json, true);
            return result;
        }
    }
}