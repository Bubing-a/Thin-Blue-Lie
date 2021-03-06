﻿using DataAccessLibrary.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThinBlueLie.ViewModels;
using static ThinBlueLie.Searches.SearchClasses;

namespace ThinBlueLie.Models.ViewModels
{
    public class EditReviewModel
    {
        public EditReviewSegment Old { get; set; }
        public EditReviewSegment New { get; set; }

        public class EditReviewSegment
        {
            public Timelineinfo Data { get; set; }
            public List<ViewMedia> Medias { get; set; }
            public List<Officer> Officers { get; set; }
            public List<Subject> Subjects { get; set; }
            public List<Misconducts> Misconducts { get; set; }
            public SimilarOfficer OfficerPerson { get; set; }
            public SimilarSubject SubjectPerson { get; set; }
        }
    }
}
