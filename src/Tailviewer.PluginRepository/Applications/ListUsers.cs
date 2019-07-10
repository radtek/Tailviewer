﻿using System;
using System.Linq;

namespace Tailviewer.PluginRepository.Applications
{
	public sealed class ListUsers
		: IApplication<ListUsersOptions>
	{
		public int Run(PluginRepository repository, ListUsersOptions options)
		{
			var users = repository.GetAllUsers().ToList();

			if (users.Any())
			{
				Console.WriteLine("There are {0} user(s):", users.Count);
				foreach (var user in users)
				{
					Console.WriteLine("\t{0}, {1}, access token: {2}", user.Username, user.Email, user.AccessToken);
				}
			}
			else
			{
				Console.WriteLine("No users have been added");
			}

			return 0;
		}
	}
}