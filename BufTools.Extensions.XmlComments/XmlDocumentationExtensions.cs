using BufTools.Extensions.XmlComments.Models;
using BufTools.Extensions.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace BufTools.Extensions.XmlComments
{
    /// <summary>
    /// A set of extension methods to ease fetching XML comments from code
    /// </summary>
    public static class XmlDocumentationExtensions
    {
        /// <summary>
        /// Load XML documentation for an assembly and parses into a data structure to later use
        /// </summary>
        /// <param name="assembly">The assembly to load docs for</param>
        /// <returns>A dictionary of the XML values</returns>
        public static IDictionary<string, MemberDoc> LoadXmlDocumentation(this Assembly assembly)
        {
            var xmlDirectoryPath = assembly.GetDirectoryPath();
            string xmlFilePath = Path.Combine(xmlDirectoryPath, assembly.GetName().Name + ".xml");

            if (!File.Exists(xmlFilePath))
                return null;

            return LoadXmlDocumentation(File.ReadAllText(xmlFilePath));
        }

        /// <summary>
        /// Load XML documentation from a file and parses into a data structure to later use
        /// </summary>
        /// <returns>A dictionary of the XML values</returns>
        public static IDictionary<string, MemberDoc> LoadXmlDocumentation(string xmlDocumentation)
        {
            var loadedXmlDocumentation = new Dictionary<string, MemberDoc>();
            using (XmlReader xmlReader = XmlReader.Create(new StringReader(xmlDocumentation)))
            {
                MemberDoc currentDoc = null;
                while (xmlReader.Read())
                {
                    if (xmlReader.Name == "member")
                    {
                        if (xmlReader.NodeType == XmlNodeType.Element)
                        {
                            currentDoc = new MemberDoc();
                            string raw_name = xmlReader["name"];
                            loadedXmlDocumentation[raw_name] = currentDoc;
                        }
                        else if (xmlReader.NodeType == XmlNodeType.Element)
                            currentDoc = null;
                    }

                    if (currentDoc != null)
                    {
                        if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "summary")
                            currentDoc.Summary = xmlReader.ReadInnerXml();

                        if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "example")
                            currentDoc.Example = xmlReader.ReadInnerXml();

                        if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "shouldpass")
                            currentDoc.PassValues.Add(xmlReader["value"]);

                        if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "shouldfail")
                            currentDoc.FailValues.Add(xmlReader["value"]);

                        if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "returns")
                            currentDoc.Returns = xmlReader.ReadInnerXml();

                        if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "param")
                        {
                            var parameter = new ParamDoc();
                            currentDoc.Params.Add(parameter);
                            parameter.Name = xmlReader["name"];
                            parameter.Example = xmlReader["example"];
                            parameter.Description = xmlReader.ReadInnerXml();
                        }

                        if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "exception")
                        {
                            var doc = new ExceptionDoc();
                            currentDoc.Exceptions.Add(doc);
                            var type = xmlReader["cref"];
                            var pos = type.IndexOf(':');

                            doc.ExceptionType = (pos > -1) ? type.Remove(0, pos + 1) : type;
                            doc.Description = xmlReader.ReadInnerXml();
                        }

                        if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "remarks")
                        {
                            var remark = xmlReader.ReadInnerXml();
                            if (!string.IsNullOrWhiteSpace(remark))
                                currentDoc.Remarks.Add(remark);
                        }
                    }
                }
            }
            return loadedXmlDocumentation;
        }

        /// <summary>
        /// Fetches documentation a class, struct, or interface
        /// </summary>
        /// <param name="xmlDocumentation">The doc source</param>
        /// <param name="type">The type to get docs for</param>
        /// <returns></returns>
        public static MemberDoc GetDocumentation(this IDictionary<string, MemberDoc> xmlDocumentation, Type type)
        {
            var key = "T:" + XmlDocumentationKeyHelper(type.FullName, null);
            xmlDocumentation.TryGetValue(key, out MemberDoc documentation);
            return documentation;
        }

        /// <summary>
        /// Fetches documentation for a property
        /// </summary>
        /// <param name="xmlDocumentation">The doc source</param>
        /// <param name="propertyInfo">The property to get docs for</param>
        /// <returns></returns>
        public static MemberDoc GetDocumentation(this IDictionary<string, MemberDoc> xmlDocumentation, PropertyInfo propertyInfo)
        {
            var key = "P:" + XmlDocumentationKeyHelper(propertyInfo.DeclaringType.FullName, propertyInfo.Name);
            xmlDocumentation.TryGetValue(key, out MemberDoc documentation);
            return documentation;
        }

        /// <summary>
        /// Fetches documentation for a method
        /// </summary>
        /// <param name="xmlDocumentation">The doc source</param>
        /// <param name="methodInfo">The method to get docs for</param>
        /// <returns></returns>
        public static MemberDoc GetDocumentation(this IDictionary<string, MemberDoc> xmlDocumentation, MethodInfo methodInfo)
        {
            string key;
            if (methodInfo.IsConstructor)
                key = "M:" + XmlDocumentationKeyHelper(methodInfo.DeclaringType.FullName, null);
            else
            {
                key = "M:" + XmlDocumentationKeyHelper(methodInfo.DeclaringType.FullName, methodInfo.Name);
                var parameters = methodInfo.GetParameters();
                if (parameters.Any())
                    key += "(" + string.Join(",", parameters.Select(p => p.ParameterType.FullName)) + ")";
            }

            xmlDocumentation.TryGetValue(key, out MemberDoc documentation);
            return documentation;
        }

        private static string XmlDocumentationKeyHelper(string typeFullNameString, string memberNameString)
        {
            string key = Regex.Replace(typeFullNameString, @"\[.*\]", string.Empty)
                              .Replace('+', '.');
            if (memberNameString != null)
                key += "." + memberNameString;

            return key;
        }
    }
}
