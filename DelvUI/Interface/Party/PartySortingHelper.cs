﻿using System;
using System.Collections.Generic;
using DelvUI.Helpers;

namespace DelvUI.Interface.Party
{
    public enum PartySortingMode
    {
        Tank_DPS_Healer = 0,
        Tank_Healer_DPS = 1,
        DPS_Tank_Healer = 2,
        DPS_Healer_Tank = 3,
        Healer_Tank_DPS = 4,
        Healer_DPS_Tank = 5
    }

    public static class PartySortingHelper
    {
        public static string[] SortingModesNames = new string[]
        {
            "Tank => DPS => Healer",
            "Tank => Healer => DPS",
            "DPS => Tank => Healer",
            "DPS => Healer => Tank",
            "Healer => Tank => DPS",
            "Healer => DPS => Tank"
        };

        public static void SortPartyMembers(ref List<IGroupMember> members, PartySortingMode mode)
        {
            members.Sort((a, b) =>
            {
                int orderA = OrderForJob(a.JobId, mode);
                int orderB = OrderForJob(b.JobId, mode);

                if (orderA == orderB)
                {
                    return a.Name.CompareTo(b.Name);
                }

                if (orderA > orderB) return -1;

                return 1;
            });
        }

        private static int OrderForJob(uint jobId, PartySortingMode mode)
        {
            var index = (int)mode;
            if (index >= Map.Count) return 0;

            var dict = Map[index];
            var role = JobsHelper.RoleForJob(jobId);

            if (dict.TryGetValue(role, out int order)) 
            {
                return order;
            }

            return 0;
        }

        private static List<Dictionary<JobRoles, int>> Map = new List<Dictionary<JobRoles, int>>() {
            new Dictionary<JobRoles, int>() {
                [JobRoles.Tank] = 3, [JobRoles.DPS] = 2, [JobRoles.Healer] = 1
            },
            new Dictionary<JobRoles, int>() {
                [JobRoles.Tank] = 3, [JobRoles.Healer] = 2, [JobRoles.DPS] = 1
            },
            new Dictionary<JobRoles, int>() {
                [JobRoles.DPS] = 3, [JobRoles.Tank] = 2, [JobRoles.Healer] = 1
            },
            new Dictionary<JobRoles, int>() {
                [JobRoles.DPS] = 3, [JobRoles.Healer] = 2, [JobRoles.Tank] = 1
            },
            new Dictionary<JobRoles, int>() {
                [JobRoles.Healer] = 3, [JobRoles.Tank] = 2, [JobRoles.DPS] = 1
            },
            new Dictionary<JobRoles, int>() {
                [JobRoles.Healer] = 3, [JobRoles.DPS] = 2, [JobRoles.Tank] = 1
            }
        };
    }
}
