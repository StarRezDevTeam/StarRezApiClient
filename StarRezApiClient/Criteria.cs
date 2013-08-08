using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace StarRezApi
{
	#region Enums

	/// <summary>
	/// The way in which criteria in a group should be related
	/// </summary>
	public enum Relationship
	{
		And,
		Or
	}

	/// <summary>
	/// The operator to use for a specific criteria
	/// </summary>
	public enum Operator
	{
		Equals,
		NotEquals,
		StartsWith,
		NotStartsWith,
		EndsWith,
		NotEndsWith,
		Contains,
		NotContains,
		In,
		NotIn,
		GreaterThan,
		GreaterThanOrEqualTo,
		LessThan,
		LessThanOrEqualTo
	}

	#endregion Enums

	#region Criteria Class

	/// <summary>
	/// Represents a single field & value criteria
	/// </summary>
	public class Criteria : ICriteria
	{
		public static Criteria Equals(string field, string value)
		{
			return new Criteria(field, Operator.Equals, value);
		}

		public static Criteria NotEquals(string field, string value)
		{
			return new Criteria(field, Operator.NotEquals, value);
		}

		public static Criteria StartsWith(string field, string value)
		{
			return new Criteria(field, Operator.StartsWith, value);
		}

		public static Criteria NotStartsWith(string field, string value)
		{
			return new Criteria(field, Operator.NotStartsWith, value);
		}

		public static Criteria EndsWith(string field, string value)
		{
			return new Criteria(field, Operator.EndsWith, value);
		}

		public static Criteria NotEndsWith(string field, string value)
		{
			return new Criteria(field, Operator.NotEndsWith, value);
		}

		public static Criteria Contains(string field, string value)
		{
			return new Criteria(field, Operator.Contains, value);
		}

		public static Criteria NotContains(string field, string value)
		{
			return new Criteria(field, Operator.NotContains, value);
		}

		public static Criteria In(string field, IEnumerable<object> value)
		{
			return new Criteria(field, Operator.In, string.Join(",", value));
		}

		public static Criteria NotIn(string field, IEnumerable<object> value)
		{
			return new Criteria(field, Operator.NotIn, string.Join(",", value));
		}

		public static Criteria GreaterThan(string field, string value)
		{
			return new Criteria(field, Operator.NotContains, value);
		}

		public static Criteria GreaterThanOrEqualTo(string field, string value)
		{
			return new Criteria(field, Operator.GreaterThanOrEqualTo, value);
		}

		public static Criteria LessThan(string field, string value)
		{
			return new Criteria(field, Operator.LessThan, value);
		}

		public static Criteria LessThanOrEqualTo(string field, string value)
		{
			return new Criteria(field, Operator.LessThanOrEqualTo, value);
		}

		public string Field { get; set; }

		public Operator Operator { get; set; }

		public string Value { get; set; }

		public Criteria(string field, Operator op, string value)
		{
			this.Field = field;
			this.Operator = op;
			this.Value = value;
		}

		public Criteria(string field, string value)
			: this(field, Operator.Equals, value)
		{
		}

		public XElement ToXml()
		{
			XElement criteria = new XElement(this.Field, this.Value);
			// Equals doesn't need to be specified.. and indeed is not allowed to be specified
			if (this.Operator != Operator.Equals)
			{
				criteria.Add(new XAttribute("_operator", this.Operator.ToString()));
			}
			return criteria;
		}
	}

	#endregion Criteria Class

	#region CriteriaGroup class

	/// <summary>
	/// Represents a group of criteria, combined together. Can also contain other groups
	/// </summary>
	public class CriteriaGroup : ICriteria
	{
		public static CriteriaGroup And(params ICriteria[] criteria)
		{
			return new CriteriaGroup(Relationship.And, criteria);
		}

		public static CriteriaGroup Or(params ICriteria[] criteria)
		{
			return new CriteriaGroup(Relationship.Or, criteria);
		}

		public Relationship Relationship { get; set; }

		public List<ICriteria> Criteria { get; private set; }

		public CriteriaGroup(Relationship relationship, params ICriteria[] criteria)
		{
			this.Criteria = new List<ICriteria>();
			this.Relationship = relationship;
			this.Criteria.AddRange(criteria);
		}

		public XElement ToXml()
		{
			XElement criteria = new XElement("_criteria");
			criteria.Add(new XElement("_relationship", this.Relationship.ToString()));
			criteria.Add(this.Criteria.Select(c => c.ToXml()).ToArray());
			return criteria;
		}
	}

	#endregion CriteriaGroup class

	#region Interfaces

	/// <summary>
	/// Represents something that makes up a criteria. Allows critera to be used directly by the service methods, and criteria groups to contain other groups
	/// </summary>
	public interface ICriteria
	{
		XElement ToXml();
	}

	#endregion Interfaces
}