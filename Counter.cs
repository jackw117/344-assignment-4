/*
 * A class to easily keep track of how many articles are in the database.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace ClassLibrary1
{
    public class Counter : TableEntity
    {
        public int count { get; set; }

        public Counter() { }

        public Counter(int previous, string partition, string row)
        {
            this.count = previous + 1;

            this.PartitionKey = partition;
            this.RowKey = row;
        }
    }
}
