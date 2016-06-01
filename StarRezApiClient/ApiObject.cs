using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace StarRezApi
{
	/// <summary>
	/// Provides a dynamic wrapper to an XML fragment that is returned by the web service. Allows a developer to get
	/// field values and related tables, and also tracks changes when fields are set.
	/// </summary>
	[DebuggerDisplay("{TableName}: {ID}")]
	public class ApiObject : System.Dynamic.DynamicObject
	{
		#region Declarations

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private XElement m_element;

		#endregion Declarations

		#region Properties

		/// <summary>
		/// Gets the name of the source table for the record that this object represents
		/// </summary>
		public string DbObjectName
		{
			get
			{
				return m_element.Name.ToString();
			}
		}

		/// <summary>
		/// Gets the primary key ID value of the record
		/// </summary>
		public string ID
		{
			get
			{
				return m_element.Element(m_element.Name + "ID").Value;
			}
		}

		#endregion Properties

		#region Constructor

		internal ApiObject(XElement element)
		{
			if (element == null) throw new ArgumentNullException("element");

			m_element = element;
		}

		#endregion Constructor

		#region DynamicObject Overrides

		/// <summary>
		/// Determines whether the specified field exists in this object.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <returns></returns>
		public bool HasField(string name)
		{
			return m_element.Elements(name).Any();
		}

		/// <summary>
		/// Gets raw values from an ApiObject array
		/// </summary>
		/// <param name="objectArray">Object array to get raw values from.</param>
		/// <returns>Raw value as string</returns>
		public string GetRaw(ApiObject[] objectArray)
		{
			return objectArray[0].m_element.Elements().First().ToString(SaveOptions.None);
		}

		public override bool TryGetMember(System.Dynamic.GetMemberBinder binder, out object result)
		{
			// Find the element or elements that match the field name requested
			IEnumerable<XElement> elements = m_element.Elements(binder.Name);
			int count = elements.Count();
			// if the element itself has child elements, then its a subtable
			if (count >= 1 && elements.First().HasElements)
			{
				result = (from x in elements
						  select new ApiObject(x)).ToArray();
				return true;
			}
			// otherwise its just a simple value
			else if (count == 1)
			{
				// unfortunately we don't know what type the field is, and .NET doesn't tell us what the
				// developer is doing with the value, so we can't do any better than a string
				result = elements.First().Value;
				return true;
			}
			return base.TryGetMember(binder, out result);
		}

		public override bool TrySetMember(System.Dynamic.SetMemberBinder binder, object value)
		{
			// Find the element that match the field name requested
			XElement element = m_element.Element(binder.Name);
			// can only set elements that don't have child elements - ie, fields not tables
			if (element != null && !element.HasElements)
			{
				// mark the element has changed, for later use
				if (element.Attribute("changed") == null)
				{
					element.Add(new XAttribute("changed", true));
				}
				// Do some simple input formatting - just dates, to make sure they travel across the wire properly
				// All other formats will be handled okay by the web service itself
				if (value is DateTime)
				{
					element.Value = ((DateTime)value).ToString("s");
				}
				else if (value is DateTime?)
				{
					if (value == null)
					{
						element.Value = "";
					}
					else
					{
						element.Value = ((DateTime?)value).Value.ToString("s");
					}
				}
				else if (value != null)
				{
					element.Value = value.ToString();
				}
				else
				{
					element.Value = "";
				}
				return true;
			}
			return base.TrySetMember(binder, value);
		}

		public override IEnumerable<string> GetDynamicMemberNames()
		{
			return m_element.Elements().Select(x => x.Name.ToString());
		}

		#endregion DynamicObject Overrides

		#region Methods

		/// <summary>
		/// Gets an Xml fragment of all of the fields that have been changed since they were loaded, including in sub-tables
		/// </summary>
		internal XElement ReduceToChanges()
		{
			XElement result = new XElement(m_element.Name);
			RecursiveLookForChanges(result, m_element);
			return result;
		}

		/// <summary>
		/// Clears the changed flag from all elements so we can start tracking again
		/// </summary>
		internal void ClearChanges()
		{
			RecursiveClearChanges(m_element);
		}

		private void RecursiveClearChanges(XElement start)
		{
			foreach (XElement element in start.Elements())
			{
				if (element.Attribute("changed") != null)
				{
					element.Attribute("changed").Remove();
				}
				else if (element.HasElements)
				{
					RecursiveClearChanges(element);
				}
			}
		}

		private void RecursiveLookForChanges(XElement result, XElement start)
		{
			foreach (XElement element in start.Elements())
			{
				if (element.Attribute("changed") != null)
				{
					result.Add(new XElement(element.Name, element.Value));
				}
				else if (element.HasElements)
				{
					XElement child = new XElement(element.Name);
					RecursiveLookForChanges(child, element);
					if (child.HasElements)
					{
						// if its a sub-table, we need to attach the ID as an attribute, so the service knows which record to update
						child.Add(new XAttribute(child.Name + "ID", element.Elements(child.Name + "ID").First().Value));
						result.Add(child);
					}
				}
			}
		}

		#endregion Methods
	}
}