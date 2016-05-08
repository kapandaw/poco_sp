using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {

            using (var db = new Rmc())
            {
                var type = new Type() { Name = "Type1" };
                db.Types.Add(type);
                db.SaveChanges();
                var transactor = new Transactor()
                {
                    State = "New2",
                    Type = type.Name,
                    Amount = 123,
                    Amount2 = 1455,
                    CreatedUser = Environment.UserName,
                    ModifiedUser = Environment.UserName,
                    TransactorName = "Client 1",

                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                };
                db.Transactors.Add(transactor);
                try
                {
                    db.SaveChanges();

                    var credit = new Credit()
                    {
                        State = "New",
                        Type = type.Name,
                        AmountSum = 12333,
                        CreatedUser = Environment.UserName,
                        ModifiedUser = Environment.UserName,
                        TransactorId = transactor.Id,
                        CreditName = "Credit 1",
                        Uid = Guid.NewGuid(),
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now
                    };
                    db.Credits.Add(credit);
                    db.SaveChanges();


                    transactor.TransactorName = "Super client 1";
                    db.SaveChanges();

                    credit.AmountSum = 4555;
                    db.SaveChanges();

                    //type.Name = "Type 2";
                    //db.SaveChanges();


                    db.Credits.Remove(credit);
                    db.SaveChanges();

                    db.Transactors.Remove(transactor);
                    db.SaveChanges();

                    db.Types.Remove(type);
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                }

                Console.WriteLine();
            }
            Console.ReadKey();
        }
    }
}
