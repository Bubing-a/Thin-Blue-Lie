﻿using AutoMapper;
using DataAccessLibrary.DataAccess;
using DataAccessLibrary.DataModels;
using DataAccessLibrary.DataModels.EditModels;
using DataAccessLibrary.Enums;
using DataAccessLibrary.UserModels;
using Ganss.XSS;
using KellermanSoftware.CompareNetObjects;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThinBlueLieB.Helper.Algorithms;
using ThinBlueLieB.Helper.Extensions;
using ThinBlueLieB.Models;
using static DataAccessLibrary.Enums.EditEnums;
using static ThinBlueLieB.Helper.ConfigHelper;
using static ThinBlueLieB.Models.SubmitBase;

namespace ThinBlueLieB.Bases
{
    public class EditBase : InformationBase
    {
        //public new SubmitModel model = new SubmitModel
        //{
        //    Timelineinfos = new ViewTimelineinfo(),
        //    Medias = new List<ViewMedia> { new ViewMedia { } },
        //    Officers = new List<ViewOfficer> { new ViewOfficer { } },
        //    Subjects = new List<ViewSubject> { new ViewSubject { } }
        //};
        internal uint Id;
        [Inject]
        SignInManager<ApplicationUser> SignInManager { get; set; }
        [Inject]
        UserManager<ApplicationUser> UserManager { get; set; }
        [Inject]
        public IMapper Mapper { get; set; }
        [Inject]
        NavigationManager navManager { get; set; }


        SubmitModel oldInfo = new SubmitModel();

        public bool EventExists = false;

        public bool EventPendingEdit = false;
        internal async Task<SubmitModel> FetchDataAsync()
        {
            DataAccess data = new DataAccess();
            string checkPending = "SELECT e.Confirmed FROM edithistory e where e.IdTimelineinfo = @id Order by e.Timestamp desc Limit 1;";
            int Confirmed = await data.LoadDataSingle<int, dynamic>(checkPending, new { id = Id }, GetConnectionString());
            if (Confirmed == 0) { 
                EventPendingEdit = true;
                return new SubmitModel { Timelineinfos = new ViewTimelineinfo { } }; 
            }
            var query = "SELECT * From timelineinfo t where t.IdTimelineinfo = @id;";
            Timelineinfo timelineinfo = await data.LoadDataSingle<Timelineinfo, dynamic>(query, new { id = Id }, GetConnectionString());
            //TODO change to multipleQueryAsync
            if (timelineinfo != null && 
                //if post is not empty and not both locked and user missing perm to edit locked
                !(timelineinfo.Locked == 1 && (User.RepAuthorizer(PrivledgeEnum.Privledges.EditLocked) == false)))
            {
                var mediaQuery = "SELECT m.IdMedia, m.MediaType, m.SourcePath, m.Gore, m.SourceFrom, m.Blurb, m.Credit, m.SubmittedBy, m.Rank From media m where m.IdTimelineinfo = @id Order By m.Rank;";
                var officerQuery = "SELECT o.IdOfficer, o.Name, o.Race, o.Sex, o.Local, o.Image, t_o.Age, t_o.Misconduct, t_o.Weapon " +
                        "FROM timelineinfo t " +
                        "JOIN timelineinfo_officer t_o ON t.IdTimelineinfo = t_o.IdTimelineinfo " +
                        "JOIN officers o ON t_o.IdOfficer = o.IdOfficer " +
                        "WHERE t.IdTimelineinfo = @id ;";
                var subjectQuery = "SELECT s.IdSubject, s.Name, s.Race, s.Sex, s.Local, s.Image, t_s.Age, t_s.Armed " +
                        "FROM timelineinfo t " +
                        "JOIN timelineinfo_subject t_s ON t.IdTimelineinfo = t_s.IdTimelineinfo " +
                        "JOIN subjects s ON t_s.IdSubject = s.IdSubject " +
                        "WHERE t.IdTimelineinfo = @id;";

                //get media, officers, and subjects using timelineinfo id
                List<ViewMedia> Media = await data.LoadData<ViewMedia, dynamic>(mediaQuery, new { id = Id }, GetConnectionString());
                List<DBOfficer> officers = await data.LoadData<DBOfficer, dynamic>(officerQuery, new { id = Id }, GetConnectionString());
                List<DBSubject> subjects = await data.LoadData<DBSubject, dynamic>(subjectQuery, new { id = Id }, GetConnectionString());
                var Info = new ViewTimelineinfo
                {
                    Date = timelineinfo.Date,
                    State = (TimelineinfoEnums.StateEnum?)timelineinfo.State,
                    City = timelineinfo.City,
                    Title = timelineinfo.Title,
                    Context = timelineinfo.Context,
                    Locked = timelineinfo.Locked,
                    SubmittedBy = timelineinfo.Owner
                };
                var Officers = Mapper.Map<List<DBOfficer>, List<ViewOfficer>>(officers);
                var Subjects = Mapper.Map<List<DBSubject>, List<ViewSubject>>(subjects);
                //Ordered by rank so it's the same as rank but it's a stupid way to do it
                for (int i = 0; i < Media.Count; i++)
                {
                    Media[i].ListIndex = i;
                }

                oldInfo = new SubmitModel
                {
                    Timelineinfos = Info,
                    Medias = Media,
                    Officers = Officers,
                    Subjects = Subjects
                };
                SubmitModel model = new SubmitModel();
                Mapper.Map(oldInfo, model);
                EventExists = true;
                return model;
            }
            EventExists = false;
            return new SubmitModel { Timelineinfos = new ViewTimelineinfo {Locked = timelineinfo.Locked } };
        }



        public EditHistory editHistory;
        async Task CreateEmptyEditHistory()
        {
            if (editHistory.ContainsChange() && (CreatedNewEditHistory == false))
            {
                string createNewEditHistory = @"INSERT INTO edithistory (`Confirmed`, `SubmittedBy`, `IdTimelineinfo`) 
                                                        VALUES ('2', @userId, @IdTimelineinfo);
                                            SELECT LAST_INSERT_ID();";
                EditHistoryId = await data.LoadDataSingle<int, dynamic>(createNewEditHistory, new { userId, IdTimelineinfo = Id }, GetConnectionString());
                CreatedNewEditHistory = true;
            }
        }
        bool CreatedNewEditHistory;

        public bool SavingData = false;

        internal AuthenticationState userState;
        DataAccess data = new DataAccess();
        int userId;
        int EditHistoryId;
        public ApplicationUser User;
        //TODO only add to junction tables is something changes. 
        internal async void HandleValidSubmitAsync()
        {
            CreatedNewEditHistory = false;
            SavingData = true;
            StateHasChanged();
            userId = Convert.ToInt32(UserManager.GetUserId(userState.User));

            editHistory = new EditHistory();
            PairVersions Pair = new PairVersions();
            CompareLogic compareLogic = new CompareLogic();

            if (!compareLogic.Compare(model.Officers, oldInfo.Officers).AreEqual)
            {
                foreach (var officer in model.Officers.Where(subject => subject.IdOfficer == 0))
                    officer.IdOfficer = int.MaxValue - new Random().Next(10000000);

                var officerPairs = Pair.PairOfficers(Mapper.Map<List<ViewOfficer>, List<DBOfficer>>(oldInfo.Officers),
                                                           Mapper.Map<List<ViewOfficer>, List<DBOfficer>>(model.Officers));
                bool ChangedJunction = false;
                foreach (var pair in officerPairs)
                {
                    //if officer changed
                    if (Mapper.Map<DBOfficer, CommonPerson>(pair.Item2).JunctionChange(Mapper.Map<DBOfficer, CommonPerson>(pair.Item1)))
                    {
                        //create new edithistory 
                        string sql = @"INSERT INTO edithistory (`SubmittedBy`, `Officers`)
                                                  VALUES (@userId, '1');
                                                  Set @editHistoryId = (Select LAST_INSERT_ID());
                                       INSERT INTO edits_officer
                                                 (`IdEditHistory`, `IdOfficer`, `Name`, `Race`, `Sex`, `Image`, `Local`, `Action`) 
                                                 VALUES (@editHistoryId, @IdOfficer, @Name, @Race, @Sex, @Image, @Local, @Action);";
                        EditActions Action;
                        if (pair.Item1 == null)
                            Action = EditActions.Addition;
                        if (pair.Item2 == null)
                            Action = EditActions.Deletion;
                        else
                            Action = EditActions.Update;
                        await data.SaveData(sql, new
                        {
                            userId = userId,
                            IdOfficer = pair.Item2.IdOfficer,
                            Name = pair.Item2.Name,
                            Race = (int)pair.Item2.Race,
                            Sex = (int)pair.Item2.Sex,
                            Image = pair.Item2.Image,
                            Local = pair.Item2.Local,
                            Action = (int)Action
                        }, GetConnectionString());
                    }
                    if ((pair.Item1?.Age != pair.Item2?.Age) || (pair.Item1?.Misconduct != pair.Item2?.Misconduct)
                        || (pair.Item1?.Weapon != pair.Item2?.Weapon))
                        ChangedJunction = true;
                }
                if (ChangedJunction)
                {
                    editHistory.Timelineinfo_Officer = 1; 
                    await CreateEmptyEditHistory();                   
                    //MySql.Data.MySqlClient.MySqlException: 'Operand should contain 1 column(s)' on line below
                    foreach (var officer in model.Officers)
                    {
                        string newTimelineinfoOfficer = $@"INSERT INTO edits_timelineinfo_officer
                                                        (`IdEditHistory`, `IdTimelineinfo`, `IdOfficer`, `Misconduct`, `Weapon`, `Age`) 
                                                        VALUES ('{EditHistoryId}', '{Id}', @IdOfficer, '{officer.Misconduct.Sum()}', '{officer.Weapon.Sum()}', @Age);";
                        await data.SaveData(newTimelineinfoOfficer, officer, GetConnectionString());
                    }                    
                }
            }
            if (!compareLogic.Compare(model.Subjects, oldInfo.Subjects).AreEqual)
            {
                foreach (var subject in model.Subjects.Where(subject => subject.IdSubject == 0))
                    subject.IdSubject = int.MaxValue - new Random().Next(10000000);

                var subjectPairs = Pair.PairSubjects(Mapper.Map<List<ViewSubject>, List<DBSubject>>(oldInfo.Subjects),
                                                           Mapper.Map<List<ViewSubject>, List<DBSubject>>(model.Subjects));
                bool ChangedJunction = false;
                foreach (var pair in subjectPairs)
                {

                    //if subject changed
                    if (Mapper.Map<DBSubject, CommonPerson>(pair.Item2).JunctionChange(Mapper.Map<DBSubject, CommonPerson>(pair.Item1)))
                    {
                        string sql = @"INSERT INTO edithistory (`SubmittedBy`, `Subjects`)
                                                  VALUES (@userId, '1');
                                                  Set @editHistoryId = (Select LAST_INSERT_ID());
                                       INSERT INTO edits_subject
                                                 (`IdEditHistory`, `IdSubject`, `Name`, `Race`, `Sex`, `Image`, `Local`, `Action`) 
                                                 VALUES (@editHistoryId, @IdSubject, @Name, @Race, @Sex, @Image, @Local, @Action);";
                        EditActions Action;
                        if (pair.Item1 == null)
                            Action = EditActions.Addition;
                        if (pair.Item2 == null)
                            Action = EditActions.Deletion;
                        else
                            Action = EditActions.Update;
                        await data.SaveData(sql, new
                        {
                            userId = userId,
                            IdSubject = pair.Item2.IdSubject,
                            Name = pair.Item2.Name,
                            Race = (int)pair.Item2.Race,
                            Sex = (int)pair.Item2.Sex,
                            Image = pair.Item2.Image,
                            Local = pair.Item2.Local,
                            Action = (int)Action
                        }, GetConnectionString());
                    }
                    //junction table has to change
                    if ((pair.Item1?.Age != pair.Item2?.Age) || (pair.Item1?.Armed != pair.Item2?.Armed))
                        ChangedJunction = true;
                }
                if (ChangedJunction)
                {
                    editHistory.Timelineinfo_Subject = 1; //triggers editHistory's set
                    await CreateEmptyEditHistory();
                    string newTimelineinfoSubject = $@"INSERT INTO edits_timelineinfo_subject
                                                        (`IdEditHistory`, `IdTimelineinfo`, `IdSubject`, `Armed`, `Age`) 
                                                        VALUES ('{EditHistoryId}', '{Id}', @IdSubject, @Armed, @Age);";
                    await data.SaveData(newTimelineinfoSubject, model.Subjects, GetConnectionString());
                }
            }
            if (!compareLogic.Compare(model.Medias, oldInfo.Medias).AreEqual)
            {
                editHistory.EditMedia = 1;
                await CreateEmptyEditHistory();
                //giving it a random id that will be used for pairing, but never inserted into database
                foreach (var media in model.Medias.Where(subject => subject.IdMedia == 0))
                    media.IdMedia = int.MaxValue - new Random().Next(10000000);
                var mediaPairs = Pair.PairMedia(Mapper.Map<List<ViewMedia>, List<Media>>(oldInfo.Medias),
                                                    Mapper.Map<List<ViewMedia>, List<Media>>(model.Medias));
                foreach (var pair in mediaPairs)
                {
                    //If there is a change
                    if (!compareLogic.Compare(pair.Item1, pair.Item2).AreEqual)
                    {
                        EditActions Action;
                        //if new media
                        if (pair.Item2 != null && pair.Item1 == null)
                        {
                            Action = EditActions.Addition;
                            string saveNewMedia = @$"INSERT INTO editmedia 
                                                      (`IdEditHistory`, `IdTimelineinfo`, `Rank`, `MediaType`, `SourcePath`,
                                                        `Gore`, `SourceFrom`, `Blurb`, `Credit`, `SubmittedBy`, `Action`)
                                                       VALUES ('{EditHistoryId}', '{Id}', @Rank, @MediaType, @SourcePath,
                                                          @Gore, @SourceFrom, @Blurb, @Credit, '{userId}', '{(int)Action}');";
                            await data.SaveData(saveNewMedia, pair.Item2, GetConnectionString());
                        }
                        //if deleted media
                        else if (pair.Item2 == null && pair.Item1 != null)
                        {
                            Action = EditActions.Deletion;
                            string deleteMedia = $@"INSERT INTO editmedia (`IdEditHistory`, `IdTimelineinfo`, `SubmittedBy`, `Action`) 
                                                    VALUES ('{EditHistoryId}', '{Id}', '{userId}', '{(int)Action}');";
                            await data.SaveData(deleteMedia, new { }, GetConnectionString());
                        }
                        //if updated
                        else
                        {
                            Action = EditActions.Update;
                            string updateMedia = $@"INSERT INTO editmedia 
                                                      (`IdEditHistory`, `IdTimelineinfo`, `IdMedia`, `Rank`, `MediaType`, `SourcePath`,
                                                        `Gore`, `SourceFrom`, `Blurb`, `Credit`, `SubmittedBy`, `Action`)
                                                       VALUES ('{EditHistoryId}', '{Id}', @IdMedia, @Rank, @MediaType, @SourcePath,
                                                          @Gore, @SourceFrom, @Blurb, @Credit, '{userId}', '{(int)Action}');";
                            await data.SaveData(updateMedia, pair.Item2, GetConnectionString());
                        }
                    }
                }
            }
            if (!compareLogic.Compare(model.Timelineinfos, oldInfo.Timelineinfos).AreEqual)
            {
                editHistory.Edits = 1;
                await CreateEmptyEditHistory();
                var sanitizer = new HtmlSanitizer();
                sanitizer.AllowedCssProperties.Remove("color");
                sanitizer.AllowedCssProperties.Remove("display");
                sanitizer.AllowedCssProperties.Remove("font-style");
                sanitizer.AllowedCssProperties.Remove("font-family");
                sanitizer.AllowedCssProperties.Remove("background-color");
                sanitizer.AllowedCssProperties.Remove("whitespace");
                model.Timelineinfos.Context = sanitizer.Sanitize(model.Timelineinfos.Context);
                string InsertEdits = $@"INSERT INTO edits (`IdEditHistory`, `IdTimelineinfo`, `Title`, `Date`, `State`, 
                                           `City`, `Context`, `Locked`) 
                                        VALUES ('{EditHistoryId}', '{Id}', @Title, @Date, @State,
                                            @City, @Context, @Locked);";
                await data.SaveData(InsertEdits, model.Timelineinfos, GetConnectionString());
            }

            string updateEditHistory = @$"UPDATE edithistory SET 
                                               `Confirmed` = '0',`Edits` = @Edits, `EditMedia` = @EditMedia,
                                                `Officers` = @Officers, Subjects` = @Subjects, `Timelineinfo_Officer` = @Timelineinfo_Officer, 
                                                `Timelineinfo_Subject` = @Timelineinfo_Subject 
                                         WHERE (`IdEditHistory` = '{EditHistoryId}');";
            await data.SaveData(updateEditHistory, editHistory, GetConnectionString());
            navManager.NavigateTo("/Account/Profile");
            SavingData = false;
        }
    }
}
