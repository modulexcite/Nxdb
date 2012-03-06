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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using NUnit.Framework;
using Nxdb;
using Nxdb.Node;
using Nxdb.Persistence;

namespace NxdbTests
{
    [TestFixture]
    public class PersistenceTest
    {
        [Test]
        public void XmlSerializerBehavior()
        {
            Common.Reset();
            using (Database database = Database.Get(Common.DatabaseName))
            {
                database.Add("Doc", "<Doc/>");
                Element elem = (Element)database.GetDocument("Doc").Child(0);
                Assert.IsNotNull(elem);

                // Initial append two items
                XmlSerializerPersistentClass foo = new XmlSerializerPersistentClass(5, "abc", false);
                XmlSerializerPersistentClass bar = new XmlSerializerPersistentClass(456, "abc", true);
                elem.Append(foo);
                elem.Append(bar, "Bar");
                string barContent = bar.ToString("Bar");
                Assert.AreEqual(foo.ToString() + barContent, elem.InnerXml);

                // Change and then store only one item
                foo.Num = 10;
                foo.Str = "xyz";
                bar.Bl = false;
                bar.Str = "qwerty";
                foo.Store();
                Assert.AreEqual(foo.ToString() + barContent, elem.InnerXml);

                // Store all items
                PersistenceManager.Default.StoreAll();
                barContent = bar.ToString("Bar");
                string fooContent = foo.ToString();
                Assert.AreEqual(fooContent + barContent, elem.InnerXml);

                // Change attached element for one item
                bar.Num = 9876;
                elem.Append(bar, "Baz");
                Assert.AreEqual(fooContent + barContent + bar.ToString("Baz"), elem.InnerXml);

                // Fetch the first instance into a new object
                XmlSerializerPersistentClass copy = new XmlSerializerPersistentClass(567, "zxcvb", true);
                Element fooElem = (Element) elem.Child(0);
                Assert.AreNotEqual(copy.ToString(), fooElem.OuterXml);
                copy.Fetch(fooElem);
                Assert.AreEqual(copy.ToString(), fooContent);
                Assert.AreEqual(copy.ToString(), fooElem.OuterXml);

                // Change the old instance and attach the new one
                foo.Str = "sprgj";
                PersistenceManager.Default.StoreAll();
                copy.Attach(fooElem);
                fooContent = foo.ToString();
                Assert.AreEqual(copy.ToString(), fooElem.OuterXml);
                Assert.AreEqual(copy.ToString(), fooContent);

                // Detach the old instance and change the new instance
                foo.Detach();
                copy.Str = "yuiop";
                PersistenceManager.Default.StoreAll();
                Assert.AreEqual(copy.ToString(), fooElem.OuterXml);
                Assert.AreEqual(fooContent, foo.ToString());
                Assert.AreNotEqual(foo.ToString(), fooElem.OuterXml);

                // Test fetch failure
                Assert.Throws<InvalidOperationException>(() => foo.Fetch((Element)fooElem.Child(0)));
            }
        }
    }

    [XmlSerializerPersistence(Indent = false)]
    public class XmlSerializerPersistentClass
    {
        private int _num;
        public int Num
        {
            get { return _num; }
            set
            {
                NumArr = new []{value+1, value+2, value+3};
                _num = value;
            }
        }
        public string Str { get; set; }

        [XmlAttribute("bool")]
        public bool Bl { get; set; }

        public int[] NumArr { get; set; }

        // The XmlSerializer requires a parameterless constructor
        public XmlSerializerPersistentClass()
        {
        }

        public XmlSerializerPersistentClass(int num, string str, bool bl) : this()
        {
            Num = num;
            Str = str;
            Bl = bl;
        }

        public string ToString(string elementName)
        {
            return String.Format(
                "<{0} bool=\"{1}\"><Num>{2}</Num><Str>{3}</Str><NumArr><int>{4}</int><int>{5}</int><int>{6}</int></NumArr></{0}>",
                elementName, Bl.ToString().ToLower(), Num, Str, Num+1, Num+2, Num+3);
        }

        public override string ToString()
        {
            return ToString("XmlSerializerPersistentClass");
        }
    }
}
