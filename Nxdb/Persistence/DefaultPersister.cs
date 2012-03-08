﻿/*
 * Copyright 2012 WildCard, LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Nxdb.Node;
using Nxdb.Persistence.Attributes;

namespace Nxdb.Persistence
{
    // TODO: Assuming that using a TypeConverterAttribute on the field/property will allow custom TypeConverters - need to verify
    // TODO: Implement support for complex object and collection types
    public class DefaultPersister : Persister
    {
        internal override void Fetch(Element element, object obj, TypeCache typeCache)
        {
            // Get all values first so if something goes wrong we haven't started modifying the object
            List<KeyValuePair<MemberInfo, object>> values
                = new List<KeyValuePair<MemberInfo, object>>();
            foreach (KeyValuePair<MemberInfo, PersistentMemberAttribute> kvp
                in typeCache.PersistentMembers.Where(kvp => kvp.Value.Fetch))
            {
                object target = GetValue(kvp.Key, obj);
                object value = kvp.Value.FetchValue(element, target, typeCache);
                values.Add(new KeyValuePair<MemberInfo, object>(kvp.Key, value));
            }

            // Now that all conversions have been succesfully performed, set the values
            foreach (KeyValuePair<MemberInfo, object> value in values)
            {
                SetValue(value.Key, obj, value.Value);
            }
        }

        // This scans over all instance fields in the object and uses a TypeConverter to convert them to string
        // If any fields cannot be converted an exception is thrown because the entire state was not stored
        internal override void Store(Element element, object obj, TypeCache typeCache)
        {
            // Get all values first so if something goes wrong we haven't started modifying the database
            List<KeyValuePair<PersistentMemberAttribute, object>> values
                = new List<KeyValuePair<PersistentMemberAttribute, object>>();
            foreach (KeyValuePair<MemberInfo, PersistentMemberAttribute> kvp
                in typeCache.PersistentMembers.Where(kvp => kvp.Value.Store))
            {
                object source = GetValue(kvp.Key, obj);
                object value = kvp.Value.GetValue(element, source, typeCache);
                values.Add(new KeyValuePair<PersistentMemberAttribute, object>(kvp.Value, value));
            }

            // Now that everything has been converted, go ahead and modify the database
            using(new Updates())
            {
                try
                {
                    foreach(KeyValuePair<PersistentMemberAttribute, object> value in values)
                    {
                        value.Key.StoreValue(element, value.Value);
                    }
                }
                catch (Exception)
                {
                    Updates.Reset();
                    throw;
                }
            }
        }

        private object GetValue(MemberInfo memberInfo, object obj)
        {
            FieldInfo fieldInfo = memberInfo as FieldInfo;
            if (fieldInfo != null) return fieldInfo.GetValue(obj);
            PropertyInfo propertyInfo = memberInfo as PropertyInfo;
            return propertyInfo != null ? propertyInfo.GetValue(obj, null) : null;
        }

        private void SetValue(MemberInfo memberInfo, object obj, object value)
        {
            FieldInfo fieldInfo = memberInfo as FieldInfo;
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(obj, value);
            }
            else
            {
                PropertyInfo propertyInfo = memberInfo as PropertyInfo;
                if(propertyInfo != null)
                {
                    propertyInfo.SetValue(obj, value, null);
                }
            }
        }
    }
}
