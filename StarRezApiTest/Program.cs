using System;
using StarRezApi;

namespace StarRezApiTest
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			// TODO: Fill in the values for the first three parameters in the StarRezApiClient constructor
			//		 The "/services" in the URL is optional
			StarRezApiClient client = new StarRezApiClient("https://www.server.com/", "Username", "Password");

			// Create a "template" for an entry object, with defaults from the database
			var entry = client.CreateDefault("Entry");

			// Fill in the property values
			entry.NameLast = "Nurk";
			entry.NameFirst = "Fred";
			entry.NameTitle = "Mr";
			entry.DOB = new DateTime(1975, 3, 5);

			// Create the entry in the database, getting the EntryID back
			int entryID = client.Create(entry);

			// select back out the entry, getting back the full details, and also getting the EntryAddress details
			entry = client.Select("Entry", entryID, includeLookupCaptions: true, relatedTables: new[] { "EntryAddress" });

			// Write out some info
			Console.WriteLine("Created a new entry with the following details:");
			Console.WriteLine("Name: " + entry.NameFirst + " " + entry.NameLast + " (" + entry.NamePreferred + ")");
			Console.WriteLine("Date of Birth: " + entry.DOB);
			Console.WriteLine("First email address: " + entry.EntryAddress[0].Email);

			// When we specify "includeLookupCaptions", any ID field in the table will have a corresponding "_Caption" field
			Console.WriteLine("Category: " + entry.CategoryID_Caption);

			// update a field of the entry
			entry.NamePreferred = "Frederick";

			// we can also update a field in another table, if we selected it out
			entry.EntryAddress[0].Email = "test@example.com";

			// save the changes to the database
			bool result = client.Update(entry);
			if (result)
			{
				Console.WriteLine("Successfully updated entry");
			}
			else
			{
				Console.WriteLine("Failed to update entry record. Result: " + client.LastResult.ToString());
			}

			// Reselect to get the new data, and redisplay
			entry = client.Select("Entry", entryID, relatedTables: new[] { "EntryAddress" });
			Console.WriteLine("Name: " + entry.NameFirst + " " + entry.NameLast + " (" + entry.NamePreferred + ")");
			Console.WriteLine("First email address: " + entry.EntryAddress[0].Email);

			// see if there are any other Nurks in the database
			var entries = client.Select("Entry", Criteria.Equals("NameLast", "Nurk"));
			Console.WriteLine("There are " + entries.Length + " nurks in the database.");

			// what about Smiths or Jones'?
			entries = client.Select("Entry", CriteriaGroup.Or(
												Criteria.Equals("NameLast", "Smith"),
												Criteria.Equals("NameLast", "Jones")
											)
									);
			// CriteriaGroups can also take other CriteriaGroups, to nest Ands and Ors as required.
			// CriteriaGroups and Criterias can also be created in the traditional manner, with "new"
			Console.WriteLine("There are " + entries.Length + " smiths and jones' in the database.");

			// delete the entry, to clean up the database
			result = client.Delete(entry);
			if (result)
			{
				Console.WriteLine("Successfully deleted entry");
			}
			else
			{
				Console.WriteLine("Failed to delete entry record. Result: " + client.LastResult.ToString());
			}
			Console.WriteLine("Press enter to continue.");
			Console.ReadLine();
		}
	}
}