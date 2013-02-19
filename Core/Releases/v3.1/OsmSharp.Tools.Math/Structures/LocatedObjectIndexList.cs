﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OsmSharp.Tools.Math.Structures
{
    /// <summary>
    /// A naive reference implementation of the ILocatedObjectIndex<PointType, DataType> interface.
    /// </summary>
    /// <typeparam name="PointType"></typeparam>
    /// <typeparam name="DataType"></typeparam>
    public class LocatedObjectIndexList<PointType, DataType> : ILocatedObjectIndex<PointType, DataType>
        where PointType : PointF2D
    {
        /// <summary>
        /// Holds a list of data.
        /// </summary>
        private List<KeyValuePair<PointType, DataType>> _data;

        /// <summary>
        /// Creates a new located object(s) index list.
        /// </summary>
        public LocatedObjectIndexList()
        {
            _data = new List<KeyValuePair<PointType, DataType>>();
        }

        /// <summary>
        /// Returns all data inside the given box.
        /// </summary>
        /// <param name="box"></param>
        /// <returns></returns>
        public IEnumerable<DataType> GetInside(GenericRectangleF2D<PointType> box)
        {
#if WINDOWS_PHONE
            // use a custom hashset implementation when using Windows Phone.
            OsmSharp.Tools.Core.Collections.HashSet<DataType> dataset = new OsmSharp.Tools.Core.Collections.HashSet<DataType>();
#else
            HashSet<DataType> dataset = new HashSet<DataType>();
#endif
            foreach (KeyValuePair<PointType, DataType> data in _data)
            {
                if (box.IsInside(data.Key))
                {
                    dataset.Add(data.Value);
                }
            }
            return dataset;
        }

        /// <summary>
        /// Adds new located data.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="data"></param>
        public void Add(PointType location, DataType data)
        {
            _data.Add(new KeyValuePair<PointType, DataType>(location, data));
        }
    }
}
