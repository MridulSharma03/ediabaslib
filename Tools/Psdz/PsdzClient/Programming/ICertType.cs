﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PsdzClient.Programming
{
    //[AuthorAPI(SelectableTypeDeclaration = true)]
    public interface ICertType
    {
        string Code { get; set; }

        string Serial { get; set; }

        string Value { get; set; }

        byte[] GetBinaryValue();
    }
}
