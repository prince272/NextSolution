﻿using MediatR;
using NextSolution.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextSolution.Core.Events.Accounts
{
    public class UserSignedOut : INotification
    {
        public UserSignedOut(User user)
        {
            User = user;
        }

        public User User { get; set; }
    }
}