﻿using DataAccessLibrary.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccessLibrary.DataModels
{
    public class DBSubject
    {
        public int IdSubject { get; set; }
        public string? Name { get; set; }
        public TimelineinfoEnums.RaceEnum Race { get; set; }
        public TimelineinfoEnums.SexEnum Sex { get; set; }
        public string? Image { get; set; }
        public byte Local { get; set; }
        public int? Age { get; set; }
        public bool Armed { get; set; }
    }
}
