using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using R4Everyone.Binary4Everyone;

namespace R4Everyone.Services;

public class DatabaseService
{
    public R4Database R4Database { get; set; } = new();
}

