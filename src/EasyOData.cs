// TODO split this file up, once we have stuff working pretty well ...

using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Requestoring;

namespace EasyOData {

	public static class XmlParsing {

		public static IEnumerable<XmlNode> GetElementsByTagName(this XmlNode node, string tagName) {
			var nodes = new List<XmlNode>();
			foreach (XmlNode child in node.ChildNodes)
				if (child.Name == tagName)
					nodes.Add(child);
			return nodes;
		}

		public static string Attr(this XmlNode node, string attributeName) {
			if (node.Attributes[attributeName] != null)
				return node.Attributes[attributeName].Value;
			else
				return null;
		}

		public static Property ToProperty(this XmlNode node) {
			return new Property {
				Name       = node.Attr("Name"),
				TypeName   = node.Attr("Type"),
				IsNullable = bool.Parse(node.Attr("Nullable"))
			};
		}

		public static EntityType ToEntityType(this XmlNode node, Metadata metadata) {
			var type = new EntityType();

			// Name
			type.Name = node.Attr("Name");

			// Properties
			foreach (XmlNode propertyNode in node.GetElementsByTagName("Property"))
				type.Properties.Add(propertyNode.ToProperty());

			// Keys
			foreach (XmlNode keyNode in node.GetElementsByTagName("Key"))
				foreach (XmlNode propertyRef in keyNode.GetElementsByTagName("PropertyRef"))
					type.Properties[propertyRef.Attr("Name")].IsKey = true;

			// Associations

			return type;
		}

		public static Collection ToCollection(this XmlNode node, Service service) {
			return new Collection {
				Name    = node["atom:title"].InnerText,
				Href    = node.Attr("href"),
				Service = service
			};
		}

	}

	public class Collection {
		public Service Service { get; set; }
		public string Name     { get; set; }
		public string Href     { get; set; }
	}

	public class Property {
		public string Name { get; set; }
		public string TypeName { get; set; } // TODO this should eventually just get { EdmType.Name }
		public bool IsNullable { get; set; }
		public bool IsKey { get; set; }

		public string Type { get { return TypeName; } } // this should be the entity type, eventually
	}

	public class PropertyList : List<Property> {
		public PropertyList() {}
		public PropertyList(IEnumerable<Property> properties) {
			AddRange(properties);
		}

		public Property this[string name] {
			get { return this.FirstOrDefault(property => property.Name == name); }
		}
	}

	public class EntityType {
		public EntityType() {
			Properties = new PropertyList();
		}

		public string Name { get; set; }
		public PropertyList Properties { get; set; }

		public List<string> PropertyNames { get { return Properties.Select(property => property.Name).ToList(); } }

		public PropertyList Keys { get { return new PropertyList(Properties.Where(p => p.IsKey)); } }
	}

	public class EntityTypeList : List<EntityType> {
		public EntityType this[string name] {
			get { return this.FirstOrDefault(type => type.Name == name); }
		}
	}

	public class Metadata {

		public Metadata(){}
		public Metadata(Service service) {
			Service = service;
		}

		public Service Service { get; set; }

		public List<string> EntityTypeNames { get { return EntityTypes.Select(t => t.Name).ToList(); } }

		public EntityTypeList EntityTypes {
			get {
				var types = new EntityTypeList();
				foreach (XmlNode node in Service.GetXml("$metadata").GetElementsByTagName("EntityType"))
					types.Add(node.ToEntityType(this));
				return types;
			}
		}

		// public List<Property> Properties {
		// 	
		// }
	}

	/// <summary>Represents an OData Service</summary>
	public class Service {

		public string Root { get; set; }

		public Service() {}
		public Service(string root) {
			Root = root;
		}

		public List<Collection> Collections {
			get {
				var collections = new List<Collection>();
				foreach (XmlNode node in GetXml("/").GetElementsByTagName("collection"))
					collections.Add(node.ToCollection(this));
				return collections;
			}
		}

		public Metadata Metadata {
			get { return new Metadata(this); }
		}

		public List<string> CollectionNames {
			get { return Collections.Select(c => c.Name).ToList(); }
		}

		#region HTTP Requesting
		Requestor _requestor = new Requestor();

		string CombineUrl(string part1, string part2) {
			return part1.TrimEnd('/') + "/" + part2.TrimStart('/');
		}

		IResponse Get(string path) {
			return _requestor.Get(CombineUrl(Root, path));
		}

		public XmlDocument GetXml(string path) {
			return GetXmlDocumentForString(Get(path).Body);
		}
		#endregion

		#region XML Parsing
		class UriSafeXmlResolver : XmlResolver {
			public override Uri ResolveUri (Uri baseUri, string relativeUri){ return baseUri; }
			public override object GetEntity (Uri absoluteUri, string role, Type type){ return null; }
			public override ICredentials Credentials { set {} }
		}

		static XmlDocument GetXmlDocumentForString(string xml) {
			var doc            = new XmlDocument();
			var reader         = new XmlTextReader(new StringReader(xml));
			reader.XmlResolver = new UriSafeXmlResolver();
			doc.Load(reader);
			return doc;
		}
		#endregion
	}
}
