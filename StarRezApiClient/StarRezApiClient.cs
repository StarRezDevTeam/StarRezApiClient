using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace StarRezApi
{
	/// <summary>
	/// A wrapper class allowing easy access to the StarRez REST API.
	/// </summary>
	/// <example>
	/// <code>
	/// StarRezApiClient client = new StarRezApiClient("https://localhost/", "user", "pass");
	/// dynamic entry = client.Select("Entry", 3);
	/// string name = entry.NameFirst + " " + entry.NameLast;
	/// entry.NameFirst = "David";
	/// if (client.Update(entry))
	/// {
	///		Log("Success!");
	/// }
	/// </code>
	/// </example>
	public class StarRezApiClient
	{
		#region Declarations

		private string m_baseUrl;
		private Dictionary<string, string> m_headers = new Dictionary<string, string>();

		#endregion Declarations

		#region Properties

		/// <summary>
		/// The username to use in requests to the API
		/// </summary>
		public string Username { get; set; }

		/// <summary>
		/// The password to use in requests to the API
		/// </summary>
		public string Password { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether or not to use windows authentication. If no username is set with windows authentication, the default (current user) credentials will be used
		/// </summary>
		public bool UseWindowsAuthentication { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether to use the legacy StarRezUsername and StarRezPassword headers for authentication. The default for this property is false, which results in using HTTP Basic authentication
		/// </summary>
		public bool UseLegacyStarRezAuthentication { get; set; }

		/// <summary>
		/// The HTTP status of the last request to the API. Provided as a reference to learn how to use the API directly.
		/// </summary>
		public HttpStatusCode LastStatus { get; private set; }

		/// <summary>
		/// The raw result XML of the last request to the API. Provided as a reference to learn how to use the API directly.
		/// </summary>
		public XElement LastResult { get; private set; }

		/// <summary>
		/// The raw request of the last request to the API. Provided as a reference to learn how to use the API directly.
		/// </summary>
		public string LastRequest { get; private set; }

		/// <summary>
		/// The last Url used by the client. Provided as a reference to learn how to use the API directly.
		/// </summary>
		public string LastUrl { get; private set; }

		#endregion Properties

		#region Constructor

		/// <summary>
		/// Creates a StarRez REST Api client at the specified base URL
		/// </summary>
		/// <param name="baseUrl">The base URL.</param>
		public StarRezApiClient(string baseUrl)
		{
			// allow all TLS versions
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

			m_baseUrl = baseUrl;
			// make sure the Url is how we expect it to be
			// Ensure a trailing slash - makes the check for "services" easier
			if (!m_baseUrl.EndsWith("/"))
			{
				m_baseUrl += "/";
			}
			// Check for services as its own directory. We include the leading slash in the check in case someone hosts at https://server/webservices/
			if (!m_baseUrl.EndsWith("/services/", StringComparison.OrdinalIgnoreCase))
			{
				m_baseUrl += "services/";
			}
		}

		/// <summary>
		/// Creates a StarRez REST Api client, with the specified username, password, and at the specified base URL
		/// </summary>
		/// <param name="baseUrl">The url of the web site hosting the services. Can include "/services" or not</param>
		/// <param name="username">The username to access the services</param>
		/// <param name="password">The password that matches the username</param>
		public StarRezApiClient(string baseUrl, string username, string password)
			: this(baseUrl)
		{
			this.Username = username;
			this.Password = password;
		}

		#endregion Constructor

		#region Methods

		/// <summary>
		/// Sets a custom header to send on all future requests.
		/// </summary>
		/// <param name="header">The header.</param>
		/// <param name="value">The value.</param>
		public void SetCustomHeader(string header, string value)
		{
			m_headers[header] = value;
		}

		#endregion Methods

		#region CreateDefault

		/// <summary>
		/// The CreateDefault method is used to return a "default" record, containing all default values as specified by the database rules
		/// </summary>
		/// <param name="tableName">The name of the database table to create a record for</param>
		/// <param name="includeLookupCaptions">Whether to include descriptive captions for all ID fields</param>
		/// <returns>The default record</returns>
		public dynamic CreateDefault(string tableName, bool includeLookupCaptions = false)
		{
			if (tableName == null) throw new ArgumentNullException("tableName");

			XElement postXml = GetSelectPostData(tableName, null, includeLookupCaptions, false, null, null, null, 0, 0, 0);

			XElement result;
			HttpStatusCode status = PerformRequest(string.Join("/", "createdefault", tableName), postXml, out result);
			if (status == HttpStatusCode.OK)
			{
				return new ApiObject(result);
			}
			return null;
		}

		#endregion CreateDefault

		#region Create

		/// <summary>
		/// The Create method saves a record to the database, and returns the ID of the new record.
		/// </summary>
		/// <param name="record">The record to create</param>
		/// <param name="autoFixErrors">Whether to automatically fix any fixable data errors</param>
		/// <param name="autoIgnoreErrors">Whether to automatically ignore any ignorable data errors</param>
		/// <param name="errorsToIgnore">A list of specific errors to ignore, if autoIgnoreErrors is off</param>
		/// <param name="errorsToFix">A list of specific errors to fix, if autoFixErrors is off</param>
		/// <param name="errorsToNotIgnore">A list of specific errors to not ignore, if autoIgnoreErrors is on</param>
		/// <param name="errorsToNotFix">A list of specific errors to not fix, if autoFixErrors is on</param>
		/// <returns>The ID of the record created</returns>
		public int Create(ApiObject record, bool autoFixErrors = true, bool autoIgnoreErrors = true, string[] errorsToIgnore = null, string[] errorsToFix = null, string[] errorsToNotIgnore = null, string[] errorsToNotFix = null)
		{
			if (record == null) throw new ArgumentNullException("record");

			XElement xml = record.ReduceToChanges();
			XElement errors = GetErrorsXml(autoFixErrors, autoIgnoreErrors, errorsToIgnore, errorsToFix, errorsToNotIgnore, errorsToNotFix);
			xml.Add(errors);
			XElement result;
			HttpStatusCode status = PerformRequest(string.Join("/", "create", record.DBObjectName), xml, out result);
			if (status == HttpStatusCode.OK)
			{
				return Convert.ToInt32(result.Element(record.DBObjectName + "ID").Value);
			}
			return -1;
		}

		#endregion Create

		#region Select

		/// <summary>
		/// Selects all records from the database for the specified table
		/// </summary>
		/// <param name="tableName">The name of the database table to create a record for</param>
		/// <param name="includeLookupCaptions">Whether to include descriptive captions for all ID fields</param>
		/// <param name="loadDeletedAndHiddenRecords">Whether to include system hidden and delete records in the results</param>
		/// <param name="orderby">A list of fields to order by. Fields should be suffixed wth ".d" to order descending</param>
		/// <param name="relatedTables">A list of related tables to include in the response</param>
		/// <param name="fields">A list of fields to return. By default all fields are returned, and some fields are always returned regardless of this value</param>
		/// <param name="top">The number of records to limit the results to</param>
		/// <param name="pageSize">Used with pageIndex, the size of the page to return</param>
		/// <param name="pageIndex">Used with pageSize, the start index of the first record in the page of data to return</param>
		/// <returns>An array of records from the database</returns>
		public dynamic[] SelectAll(string tableName, bool includeLookupCaptions = false, bool loadDeletedAndHiddenRecords = false, string[] orderby = null, string[] relatedTables = null, string[] fields = null, int top = 0, int pageSize = 0, int pageIndex = 0)
		{
			if (tableName == null) throw new ArgumentNullException("tableName");

			XElement postXml = GetSelectPostData(tableName, null, includeLookupCaptions, loadDeletedAndHiddenRecords, orderby, relatedTables, fields, top, pageSize, pageIndex);

			// if none of the options were used, then we need to tell the service to specifically select everything
			if (!postXml.HasElements)
			{
				postXml.Add(new XElement("_loadAll", true));
			}

			return PerformSelect(tableName, -1, postXml);
		}

		/// <summary>
		/// Selects a single record from the database, by its primary key
		/// </summary>
		/// <param name="tableName">The name of the database table to create a record for</param>
		/// <param name="id">The ID of the record to return</param>
		/// <param name="includeLookupCaptions">Whether to include descriptive captions for all ID fields</param>
		/// <param name="loadDeletedAndHiddenRecords">Whether to include system hidden and delete records in the results</param>
		/// <param name="relatedTables">A list of related tables to include in the response</param>
		/// <param name="fields">A list of fields to return. By default all fields are returned, and some fields are always returned regardless of this value</param>
		/// <returns>A record from the database</returns>
		public dynamic Select(string tableName, int id, bool includeLookupCaptions = false, bool loadDeletedAndHiddenRecords = false, string[] relatedTables = null, string[] fields = null)
		{
			if (tableName == null) throw new ArgumentNullException("tableName");
			if (id < 0) throw new ArgumentOutOfRangeException("id", id, "ID must be greater than or equal to 0");

			XElement postXml = GetSelectPostData(tableName, null, includeLookupCaptions, loadDeletedAndHiddenRecords, null, relatedTables, fields, 0, 0, 0);

			dynamic[] results = PerformSelect(tableName, id, postXml);
			if (results == null || results.Length == 0)
			{
				return null;
			}
			else
			{
				return results[0];
			}
		}

		/// <summary>
		/// Selects all records from the database that match the specified criteria
		/// </summary>
		/// <param name="tableName">The name of the database table to create a record for</param>
		/// <param name="criteria">The criteria with which to query the database</param>
		/// <param name="includeLookupCaptions">Whether to include descriptive captions for all ID fields</param>
		/// <param name="loadDeletedAndHiddenRecords">Whether to include system hidden and delete records in the results</param>
		/// <param name="orderby">A list of fields to order by. Fields should be suffixed wth ".d" to order descending</param>
		/// <param name="relatedTables">A list of related tables to include in the response</param>
		/// <param name="fields">A list of fields to return. By default all fields are returned, and some fields are always returned regardless of this value</param>
		/// <param name="top">The number of records to limit the results to</param>
		/// <param name="pageSize">Used with pageIndex, the size of the page to return</param>
		/// <param name="pageIndex">Used with pageSize, the start index of the first record in the page of data to return</param>
		/// <returns>An array of records from the database</returns>
		public dynamic[] Select(string tableName, ICriteria criteria, bool includeLookupCaptions = false, bool loadDeletedAndHiddenRecords = false, string[] orderby = null, string[] relatedTables = null, string[] fields = null, int top = 0, int pageSize = 0, int pageIndex = 0)
		{
			if (tableName == null) throw new ArgumentNullException("tableName");
			if (criteria == null) throw new ArgumentNullException("criteria");

			XElement postXml = GetSelectPostData(tableName, criteria, includeLookupCaptions, loadDeletedAndHiddenRecords, orderby, relatedTables, fields, top, pageSize, pageIndex);

			return PerformSelect(tableName, -1, postXml);
		}

		private dynamic[] PerformSelect(string tableName, int id, XElement postXml)
		{
			List<object> urlBits = new List<object> { "select", tableName };
			if (id > -1)
			{
				urlBits.Add(id);
			}

			XElement result;
			HttpStatusCode status = PerformRequest(string.Join("/", urlBits), postXml, out result);
			if (status == HttpStatusCode.OK)
			{
				return result.Elements(tableName).Select(x => new ApiObject(x)).ToArray();
			}
			return null;
		}

		#endregion Select

		#region Update

		/// <summary>
		/// Updates existing data in the database, by saving the changes made to the specified record
		/// </summary>
		/// <param name="record">The record to update</param>
		/// <param name="autoFixErrors">Whether to automatically fix any fixable data errors</param>
		/// <param name="autoIgnoreErrors">Whether to automatically ignore any ignorable data errors</param>
		/// <param name="errorsToIgnore">A list of specific errors to ignore, if autoIgnoreErrors is off</param>
		/// <param name="errorsToFix">A list of specific errors to fix, if autoFixErrors is off</param>
		/// <param name="errorsToNotIgnore">A list of specific errors to not ignore, if autoIgnoreErrors is on</param>
		/// <param name="errorsToNotFix">A list of specific errors to not fix, if autoFixErrors is on</param>
		/// <returns>True if the record was succesfully updated</returns>
		public bool Update(ApiObject record, bool autoFixErrors = true, bool autoIgnoreErrors = true, string[] errorsToIgnore = null, string[] errorsToFix = null, string[] errorsToNotIgnore = null, string[] errorsToNotFix = null)
		{
			if (record == null) throw new ArgumentNullException("record");

			XElement xml = record.ReduceToChanges();
			XElement errors = GetErrorsXml(autoFixErrors, autoIgnoreErrors, errorsToIgnore, errorsToFix, errorsToNotIgnore, errorsToNotFix);
			xml.Add(errors);
			XElement result;
			HttpStatusCode status = PerformRequest(string.Join("/", "update", record.DBObjectName, record.ID), xml, out result);
			if (status == HttpStatusCode.OK)
			{
				record.ClearChanges();
				return true;
			}
			return false;
		}

		#endregion Update

		#region Delete

		/// <summary>
		/// Deletes existing data in the database, by deleting the specified record
		/// </summary>
		/// <param name="record">The record to update</param>
		/// <param name="autoFixErrors">Whether to automatically fix any fixable data errors</param>
		/// <param name="autoIgnoreErrors">Whether to automatically ignore any ignorable data errors</param>
		/// <param name="errorsToIgnore">A list of specific errors to ignore, if autoIgnoreErrors is off</param>
		/// <param name="errorsToFix">A list of specific errors to fix, if autoFixErrors is off</param>
		/// <param name="errorsToNotIgnore">A list of specific errors to not ignore, if autoIgnoreErrors is on</param>
		/// <param name="errorsToNotFix">A list of specific errors to not fix, if autoFixErrors is on</param>
		/// <returns>True if the record was succesfully deleted</returns>
		public bool Delete(ApiObject record, bool autoFixErrors = true, bool autoIgnoreErrors = true, string[] errorsToIgnore = null, string[] errorsToFix = null, string[] errorsToNotIgnore = null, string[] errorsToNotFix = null)
		{
			if (record == null) throw new ArgumentNullException("record");

			return Delete(record.DBObjectName, Convert.ToInt32(record.ID), autoFixErrors, autoIgnoreErrors, errorsToIgnore, errorsToFix, errorsToNotIgnore, errorsToNotFix);
		}

		/// <summary>
		/// Deletes existing data in the database, by deleting the specified record
		/// </summary>
		/// <param name="tableName">The table to delete from</param>
		/// <param name="id">The ID of the record to delete</param>
		/// <param name="autoFixErrors">Whether to automatically fix any fixable data errors</param>
		/// <param name="autoIgnoreErrors">Whether to automatically ignore any ignorable data errors</param>
		/// <param name="errorsToIgnore">A list of specific errors to ignore, if autoIgnoreErrors is off</param>
		/// <param name="errorsToFix">A list of specific errors to fix, if autoFixErrors is off</param>
		/// <param name="errorsToNotIgnore">A list of specific errors to not ignore, if autoIgnoreErrors is on</param>
		/// <param name="errorsToNotFix">A list of specific errors to not fix, if autoFixErrors is on</param>
		/// <returns>True if the record was succesfully deleted</returns>
		public bool Delete(string tableName, int id, bool autoFixErrors = true, bool autoIgnoreErrors = true, string[] errorsToIgnore = null, string[] errorsToFix = null, string[] errorsToNotIgnore = null, string[] errorsToNotFix = null)
		{
			if (tableName == null) throw new ArgumentNullException("tableName");
			if (id < 0) throw new ArgumentOutOfRangeException("id", id, "ID must be greater than or equal to 0");

			XElement errors = GetErrorsXml(autoFixErrors, autoIgnoreErrors, errorsToIgnore, errorsToFix, errorsToNotIgnore, errorsToNotFix);

			XElement root = new XElement(tableName);
			root.Add(errors);
			XElement result;
			HttpStatusCode status = PerformRequest(string.Join("/", "delete", tableName, id), root, out result);
			return (status == HttpStatusCode.OK);
		}

		#endregion Delete

		#region Function

		public dynamic CheckIn(int id)
		{
			if (id < 0) throw new ArgumentOutOfRangeException("id", id, "ID must be greater than or equal to 0");

			XElement postXml = GetCheckinOutPostData("CheckIn");

			dynamic[] results = CheckInOut(id, postXml);
			if (results == null || results.Length == 0)
			{
				return null;
			}
			else
			{
				return results[0];
			}

		}

		public dynamic CheckOut(int id)
		{
			if (id < 0) throw new ArgumentOutOfRangeException("id", id, "ID must be greater than or equal to 0");

			XElement postXml = GetCheckinOutPostData("CheckOut");

			dynamic[] results = CheckInOut(id, postXml);
			if (results == null || results.Length == 0)
			{
				return null;
			}
			else
			{
				return results[0];
			}

		}


		private dynamic[] CheckInOut(int id, XElement postXml)
		{
			List<object> urlBits = new List<object> { "function", "entry" };
			if (id > -1)
			{
				urlBits.Add(id);
				urlBits.Add("CheckInOut");
			}

			XElement result;
			HttpStatusCode status = PerformRequest(string.Join("/", urlBits), postXml, out result);
			if (status == HttpStatusCode.OK)
			{
				return result.Elements("CheckInOut").Select(x => new ApiObject(x)).ToArray();
			}
			return null;
		}


		#endregion Function

		#region GetReport

		/// <summary>
		/// Returns the data from a report
		/// </summary>
		/// <param name="reportID">The ID of the report</param>
		/// <param name="criteria">The parameters with which to query the report if any</param>
		/// <returns>An array of records from the database</returns>
		public dynamic[] GetReport(int reportID, ICriteria criteria = null)
		{
			return GetReport(reportID.ToString(), criteria);
		}

		/// <summary>
		/// Returns the data from a report
		/// </summary>
		/// <param name="reportName">The name of the report to return or the ID of the report as a string</param>
		/// <param name="criteria">The parameters with which to query the report if any</param>
		/// <returns>An array of records from the database</returns>
		public dynamic[] GetReport(string reportName, ICriteria criteria = null)
		{
			if (reportName == null) throw new ArgumentNullException("reportName");

			XElement postXml = null;
			if (criteria != null)
			{
				postXml = new XElement("Parameters");
				postXml.Add(criteria.ToXml());
			}

			List<string> urlBits = new List<string> { "getreport", reportName };

			XElement result;
			HttpStatusCode status = PerformRequest(string.Join("/", urlBits), postXml, out result);
			if (status == HttpStatusCode.OK)
			{
				return result.Elements("Record").Select(x => new ApiObject(x)).ToArray();
			}
			return null;
		}

		#endregion GetReport

		#region Query

		public dynamic[] Query(string query)
		{
			if (query == null) throw new ArgumentNullException("query");

			XElement result;
			HttpStatusCode status = PerformRequest(string.Join("/", "query"), query, out result);
			if (status == HttpStatusCode.OK)
			{
				return result.Elements("Record").Select(x => new ApiObject(x)).ToArray();
			}
			return null;
		}

		#endregion Query

		#region Private Helper Methods

		private XElement GetSelectPostData(string tableName, ICriteria criteria, bool includeLookupCaptions, bool loadDeletedAndHiddenRecords, string[] orderby, string[] relatedTables, string[] fields, int top, int pageSize, int pageIndex)
		{
			XElement root = new XElement(tableName);
			if (criteria != null)
			{
				root.Add(criteria.ToXml());
			}
			if (loadDeletedAndHiddenRecords)
			{
				root.Add(new XElement("_loadDeletedAndHiddenRecords", true));
			}
			if (includeLookupCaptions)
			{
				root.Add(new XElement("_includeLookupCaptions", true));
			}
			if (top > 0)
			{
				root.Add(new XElement("_top", top));
			}
			if (pageIndex > 0)
			{
				root.Add(new XElement("_pageIndex", pageIndex));
			}
			if (pageSize > 0)
			{
				root.Add(new XElement("_pageSize", pageSize));
			}
			if (relatedTables != null && relatedTables.Length > 0)
			{
				root.Add(new XElement("_relatedTables", string.Join(",", relatedTables)));
			}
			if (orderby != null && orderby.Length > 0)
			{
				root.Add(new XElement("_orderBy", string.Join(",", orderby)));
			}
			return root;
		}

		private XElement GetCheckinOutPostData(string CheckInOut)
		{
			XElement root = new XElement("Parameters");
			root.Add(new XElement("operation", CheckInOut));

			return root;
		}

		private XElement GetErrorsXml(bool autoFixErrors, bool autoIgnoreErrors, string[] errorsToIgnore, string[] errorsToFix, string[] errorsToNotIgnore, string[] errorsToNotFix)
		{
			XElement errors = new XElement("_error");
			errors.Add(new XElement("_autoFix", autoFixErrors));
			errors.Add(new XElement("_autoIgnore", autoIgnoreErrors));
			if (errorsToIgnore != null && errorsToIgnore.Length > 0)
			{
				errors.Add(errorsToIgnore.Select(e => new XElement("ignore", e)).ToArray());
			}
			if (errorsToFix != null && errorsToFix.Length > 0)
			{
				errors.Add(errorsToFix.Select(e => new XElement("fix", e)).ToArray());
			}
			if (errorsToNotIgnore != null && errorsToNotIgnore.Length > 0)
			{
				errors.Add(errorsToNotIgnore.Select(e => new XElement("dontignore", e)).ToArray());
			}
			if (errorsToNotFix != null && errorsToNotFix.Length > 0)
			{
				errors.Add(errorsToNotFix.Select(e => new XElement("dontfix", e)).ToArray());
			}
			return errors;
		}

		private HttpStatusCode PerformRequest(string url, XElement postXml, out XElement result)
		{
			string postData = postXml == null ? null : postXml.ToString();
			return PerformRequest(url, postData, out result);
		}

		private HttpStatusCode PerformRequest(string url, string postData, out XElement result)
		{
			result = null;
			HttpStatusCode status = HttpStatusCode.BadRequest;
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(m_baseUrl + url);

			if (!string.IsNullOrEmpty(this.Username))
			{
				if (this.UseWindowsAuthentication)
				{
					req.Credentials = new NetworkCredential(this.Username, this.Password);
				}
				else if (this.UseLegacyStarRezAuthentication)
				{
					req.Headers.Add("StarRezUsername", this.Username);
					req.Headers.Add("StarRezPassword", this.Password);
				}
				else
				{
					req.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(this.Username + ":" + this.Password));
				}
			}
			else if (this.UseWindowsAuthentication)
			{
				// blank username and windows auth means to use default credentials
				req.UseDefaultCredentials = true;
			}
			foreach (string key in m_headers.Keys)
			{
				req.Headers.Add(key, m_headers[key]);
			}

			req.Method = postData == null ? "GET" : "POST";
			req.ContentType = "text/xml";
			req.Accept = "text/xml";
			try
			{
				if (postData != null)
				{
					using (StreamWriter writer = new StreamWriter(req.GetRequestStream()))
					{
						writer.Write(postData);
					}
				}
			}
			catch (WebException ex)
			{
				HttpWebResponse response = ex.Response as HttpWebResponse;
				if (response != null)
				{
					using (StreamReader reader = new StreamReader(response.GetResponseStream()))
					{
						result = XElement.Parse(reader.ReadToEnd());
						status = response.StatusCode;
					}
				}
				else
				{
					result = new XElement("error", new XElement("description", ex.ToString()));
					status = HttpStatusCode.BadRequest;
				}
			}
			catch (Exception ex)
			{
				result = new XElement("error", new XElement("description", ex.ToString()));
				status = HttpStatusCode.BadRequest;
			}
			if (result == null)
			{
				try
				{
					HttpWebResponse response = req.GetResponse() as HttpWebResponse;
					using (StreamReader reader = new StreamReader(response.GetResponseStream()))
					{
						result = XElement.Parse(reader.ReadToEnd());
					}
					status = response.StatusCode;
				}
				catch (WebException ex)
				{
					HttpWebResponse response = ex.Response as HttpWebResponse;
					if (response != null)
					{
						using (StreamReader reader = new StreamReader(response.GetResponseStream()))
						{
							result = XElement.Parse(reader.ReadToEnd());
						}
						status = response.StatusCode;
					}
					else
					{
						result = new XElement("error", new XElement("description", ex.ToString()));
						status = HttpStatusCode.BadRequest;
					}
				}
				catch (Exception ex)
				{
					result = new XElement("error", new XElement("description", ex.ToString()));
					status = HttpStatusCode.BadRequest;
				}
			}

			this.LastResult = result;
			this.LastStatus = status;
			this.LastRequest = postData;
			this.LastUrl = m_baseUrl + url;

			return status;
		}

		#endregion Private Helper Methods
	}
}