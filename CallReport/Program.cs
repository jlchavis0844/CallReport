using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using unirest_net.http;
using System.Data.SqlClient;
using System.Data;

namespace ContactReport {
    class Program {
        public static Dictionary<int, string> ownersData = new Dictionary<int, string>();
        public static Dictionary<int, string> outcomeData = new Dictionary<int, string>();
        public static List<Owner> owners;
        public static List<Call> conList;
        public static string token = "";
        private static Random random = new Random();
        private static string line = @"INSERT INTO CallStats ([name], [user_id], [count], [first], [last], [avg], [insertedby], [startDate], [endDate], [hash]) " +
                                     "VALUES (@name, @user_id, @count, @first, @last, @avg, @insertedby, @startDate, @endDate, @hash);";

        static void Main(string[] args) {
            string startURL = @"https://api.getbase.com/v2/calls?per_page=100";
            owners = new List<Owner>();
            conList = new List<Call>();
            var fs = new FileStream(@"C:\apps\NiceOffice\token", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            using (var sr = new StreamReader(fs)) {
                token = sr.ReadToEnd();
            }

            string hash = RandomString(10);
            string me = Environment.UserDomainName.ToString() + @"\" + Environment.UserName;

            SetOwnerData();
            SetOutcomeData();

            string testJSON = Get(startURL, token);
            DateTime now = DateTime.Now;
            DateTime lastWeek = DateTime.Now.Date.AddDays(-7);

            Console.WriteLine(lastWeek + " to " + now);

            JObject jsonObj = JObject.Parse(testJSON) as JObject;
            var jArr = jsonObj["items"] as JArray;

            foreach (var v in jArr) {
                var data = v["data"];

                bool missed = true;
                if (data["missed"] == null && data["duration"] != null &&
                    Convert.ToInt32(data["duration"]) > 0) {
                    missed = false;
                }
                else if (data["missed"] != null && data["missed"].ToString() != "") {
                    missed = Convert.ToBoolean(data["missed"]);
                }

                if (missed) {
                    continue;
                }
                bool incoming = Convert.ToBoolean(data["incoming"]);
                if (incoming) {
                    continue;
                }
                int id = Convert.ToInt32(data["id"]);
                int user_id = Convert.ToInt32(data["user_id"]);
                DateTime made_at = Convert.ToDateTime(data["made_at"]);
                DateTime updated_at = Convert.ToDateTime(data["updated_at"]);
                string name = ownersData[user_id];
                string resource_type = data["resource_type"].ToString();
                int outcome_id = 0;

                if (data["outcome_id"] != null && data["outcome_id"].ToString() != "") {
                    outcome_id = Convert.ToInt32(data["outcome_id"]);
                }

                string outcome = outcomeData[outcome_id];

                int duration = 0;
                if(data["duration"] != null && data["duration"].ToString() != "") {
                    duration = Convert.ToInt32(data["duration"]);
                }
                                
                if (made_at >= lastWeek) {
                    conList.Add(new Call(id, user_id, made_at, updated_at, name, resource_type, outcome, duration, incoming, missed));
                }
                else {
                    Console.WriteLine(made_at + " is too old");
                    break;
                }
            }

            string nextURL = jsonObj["meta"]["links"]["next_page"].ToString();
            while (Convert.ToDateTime(jArr.Last["data"]["made_at"]) >= lastWeek) {
                Console.WriteLine(Convert.ToDateTime(jArr.Last["data"]["made_at"]) + " >= " + lastWeek);
                jsonObj = JObject.Parse(Get(nextURL, token)) as JObject;
                nextURL = jsonObj["meta"]["links"]["next_page"].ToString();
                jArr = jsonObj["items"] as JArray;

                foreach (var v in jArr) {
                    var data = v["data"];
                    bool missed = true;
                    if(data["missed"] == null && data["duration"] != null && 
                        Convert.ToInt32(data["duration"]) > 0){
                        missed = false;
                    } else if(data["missed"] != null && data["missed"].ToString() != "") {
                        missed = Convert.ToBoolean(data["missed"]);
                    }

                    if (missed) {
                        continue;
                    }

                    int id = Convert.ToInt32(data["id"]);
                    int user_id = Convert.ToInt32(data["user_id"]);
                    DateTime made_at = Convert.ToDateTime(data["made_at"]);
                    DateTime updated_at = Convert.ToDateTime(data["updated_at"]);
                    string name = ownersData[user_id];
                    string resource_type = data["resource_type"].ToString();
                    int outcome_id = 0;

                    if (data["outcome_id"] != null && data["outcome_id"].ToString() != "") {
                        outcome_id = Convert.ToInt32(data["outcome_id"]);
                    }

                    string outcome = outcomeData[outcome_id];

                    int duration = 0;
                    if (data["duration"] != null && data["duration"].ToString() != "") {
                        duration = Convert.ToInt32(data["duration"]);
                    }
                    bool incoming = Convert.ToBoolean(data["incoming"]);


                    if (made_at >= lastWeek) {
                        conList.Add(new Call(id, user_id, made_at, updated_at, name, resource_type, outcome, duration, incoming, missed));
                    }
                    else {
                        Console.WriteLine(made_at + " is too old");
                        break;
                    }
                }
            }

            var statsList = (
                from con in conList
                group con by con.user_id).ToList();

            StreamWriter file = new StreamWriter("H:\\Desktop\\CallDataLine.csv");
            file.WriteLine("name,user_id,count,First,Last,Avg");
            List<Object[]> inserts = new List<Object[]>();

            foreach (var item in statsList) {
                int count = conList.Where(e => e.user_id == item.Key).Count();
                int cID = item.Key;
                string cName = item.ToList()[0].name;
                DateTime first = conList.Where(e => e.user_id == item.Key).Min(ca => ca.made_at);
                DateTime last = conList.Where(e => e.user_id == item.Key).Max(ca => ca.made_at);
                double avg = conList.Where(e => e.user_id == item.Key).Average(ca => ca.duration);
                Object[] tArr = { cName, cID, count, first, last, avg, me, lastWeek, now, hash };
                inserts.Add(tArr);
                file.WriteLine(cName + "," + cID + "," + count + "," + first + "," + last + "," + avg);
            }

            file.Flush();
            file.Close();

            string connString = "Data Source=RALIMSQL1;Initial Catalog=CAMSRALFG;Integrated Security=SSPI;";

            using (SqlConnection connection = new SqlConnection(connString)) {

                foreach (Object[] oArr in inserts) {
                    using (SqlCommand command = new SqlCommand(line, connection)) {
                        command.Parameters.Add("@name", SqlDbType.NVarChar).Value = oArr[0];
                        command.Parameters.Add("@user_id", SqlDbType.Int).Value = oArr[1];
                        command.Parameters.Add("@count", SqlDbType.Int).Value = oArr[2];
                        command.Parameters.Add("@first", SqlDbType.DateTime).Value = oArr[3];
                        command.Parameters.Add("@last", SqlDbType.DateTime).Value = oArr[4];
                        command.Parameters.Add("@avg", SqlDbType.Float).Value = oArr[5];
                        command.Parameters.Add("@insertedby", SqlDbType.NVarChar).Value = oArr[6];
                        command.Parameters.Add("@startDate", SqlDbType.DateTime).Value = oArr[7];
                        command.Parameters.Add("@endDate", SqlDbType.DateTime).Value = oArr[8];
                        command.Parameters.Add("@hash", SqlDbType.NVarChar).Value = oArr[9];

                        try {
                            connection.Open();

                            int result = command.ExecuteNonQuery();

                            if (result < 0) {
                                Console.WriteLine("INSERT failed for " + command.ToString());
                            }
                        }
                        catch (Exception ex) {
                            Console.WriteLine(ex);
                        }
                        finally {
                            connection.Close();
                        }
                    }
                }
            }

            Console.WriteLine("Done!");
            Console.ReadLine();
        }

        public static string Get(string url, string token) {
            string body = "";
            try {
                HttpResponse<string> jsonReponse = Unirest.get(url)
                    .header("accept", "application/json")
                    .header("Authorization", "Bearer " + token)
                    .asJson<string>();
                body = jsonReponse.Body.ToString();
                return body;
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                return body;
            }
        }

        public static void SetOwnerData() {
            string testJSON = Get(@"https://api.getbase.com/v2/users?per_page=100&sort_by=created_at&status=active", token);
            JObject jObj = JObject.Parse(testJSON) as JObject;
            JArray jArr = jObj["items"] as JArray;
            foreach (var obj in jArr) {
                var data = obj["data"];
                int tID = Convert.ToInt32(data["id"]);
                string tName = data["name"].ToString();
                ownersData.Add(tID, tName);
            }
        }

        public static void SetOutcomeData() {
            string testJSON = Get(@"https://api.getbase.com/v2/call_outcomes?per_page=100", token);
            JObject jObj = JObject.Parse(testJSON) as JObject;
            JArray jArr = jObj["items"] as JArray;
            outcomeData.Add(0, "Unknown");

            foreach (var obj in jArr) {
                var data = obj["data"];
                int tID = Convert.ToInt32(data["id"]);
                string tName = data["name"].ToString();
                outcomeData.Add(tID, tName);
            }
        }

        public static string RandomString(int length) {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
