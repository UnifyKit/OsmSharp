﻿// OsmSharp - OpenStreetMap (OSM) SDK
// Copyright (C) 2013 Abelshausen Ben
// 
// This file is part of OsmSharp.
// 
// OsmSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// OsmSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with OsmSharp. If not, see <http://www.gnu.org/licenses/>.

using OsmSharp.Collections.Tags;
using OsmSharp.Geo.Attributes;
using OsmSharp.Geo.Features;
using OsmSharp.Geo.Geometries;
using OsmSharp.Math.Geo;
using OsmSharp.Units.Distance;
using OsmSharp.Units.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace OsmSharp.Routing
{
    /// <summary>
    /// Represents a route generated by OsmSharp.
    /// </summary>
    public class Route
    {
        /// <summary>
        /// Creates a new route.
        /// </summary>
        public Route()
        { 
            this.TimeStamp = DateTime.Now;
        }

        /// <summary>
        /// The name of the vehicle that this route was calculated for.
        /// </summary>
        /// <remarks>This vehicle name is empty for multimodal routes.</remarks>
        public string Vehicle { get; set; }

        /// <summary>
        /// Tags for this route.
        /// </summary>
        public RouteTags[] Tags { get; set; }

        /// <summary>
        /// A number of route metrics, usually containing time/distance.
        /// </summary>
        /// <remarks>Can also be use for CO2 calculations or quality estimates.</remarks>
        public RouteMetric[] Metrics { get; set; }
        
        /// <summary>
        /// An ordered array of route entries reprenting the details of the route to the next
        /// route point.
        /// </summary>
        public RouteSegment[] Segments { get; set; }

        /// <summary>
        /// The time this route was created.
        /// </summary>
        public DateTime TimeStamp { get; set; }

        #region Save / Load

        /// <summary>
        /// Saves a serialized version to a stream.
        /// </summary>
        /// <param name="stream"></param>
        public void Save(Stream stream)
        {
            var ser = new XmlSerializer(typeof(Route));
            ser.Serialize(stream, this);
            stream.Flush();
        }

        /// <summary>
        /// Saves the route as a byte stream.
        /// </summary>
        /// <returns></returns>
        public byte[] SaveToByteArray()
        {
            using(var memoryStream = new MemoryStream())
            {
                this.Save(memoryStream);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Reads a route from a data stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static Route Load(Stream stream)
        {
            var ser = new XmlSerializer(typeof(Route));
            return ser.Deserialize(stream) as Route;
        }

        /// <summary>
        /// Parses a route from a byte array.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static Route Load(byte[] bytes)
        {
            using(var memoryStream = new MemoryStream(bytes))
            {
                var serializer = new XmlSerializer(typeof(Route));
                return (serializer.Deserialize(memoryStream) as Route);
            }
        }

        /// <summary>
        /// Save the route as GPX.
        /// </summary>
        /// <param name="stream"></param>
        public void SaveAsGpx(Stream stream)
        {
            OsmSharp.Routing.Gpx.RouteGpx.Save(stream, this);
        }

        /// <summary>
        /// Save the route as GeoJson.
        /// </summary>
        /// <param name="stream"></param>
        public void SaveAsGeoJson(Stream stream)
        {
            var streamWriter = new StreamWriter(stream);
            streamWriter.Write(this.ToGeoJson());
            streamWriter.Flush();
        }

        /// <summary>
        /// Returns this route in GeoJson.
        /// </summary>
        /// <returns></returns>
        public string ToGeoJson()
        {
            return OsmSharp.Geo.Streams.GeoJson.GeoJsonConverter.ToGeoJson(this.ToFeatureCollection());
        }

        /// <summary>
        /// Converts this route to a feature collection.
        /// </summary>
        /// <returns></returns>
        public FeatureCollection ToFeatureCollection()
        {
            var featureCollection = new FeatureCollection();
            for (int i = 0; i < this.Segments.Length; i++)
            {
                // create a line string for the current segment.
                if (i > 0)
                { // but only do so when there is a previous point available.
                    var segmentLineString = new LineString(
                        new GeoCoordinate(this.Segments[i - 1].Latitude, this.Segments[i - 1].Longitude),
                        new GeoCoordinate(this.Segments[i].Latitude, this.Segments[i].Longitude));

                    var segmentTags = this.Segments[i].Tags;
                    var attributesTable = new SimpleGeometryAttributeCollection();
                    if (segmentTags != null)
                    { // there are tags.
                        foreach (var tag in segmentTags)
                        {
                            attributesTable.Add(tag.Key, tag.Value);
                        }
                    }
                    attributesTable.Add("time", this.Segments[i].Time);
                    attributesTable.Add("distance", this.Segments[i].Distance);
                    if (this.Segments[i].Vehicle != null)
                    {
                        attributesTable.Add("vehicle", this.Segments[i].Vehicle);
                    }
                    featureCollection.Add(new Feature(segmentLineString, attributesTable));
                }

                // create points.
                if (this.Segments[i].Points != null)
                {
                    foreach (var point in this.Segments[i].Points)
                    {
                        // build attributes.
                        var currentPointTags = point.Tags;
                        var attributesTable = new SimpleGeometryAttributeCollection();
                        if (currentPointTags != null)
                        { // there are tags.
                            foreach (var tag in currentPointTags)
                            {
                                attributesTable.Add(tag.Key, tag.Value);
                            }
                        }

                        // build feature.
                        var pointGeometry = new Point(new GeoCoordinate(point.Latitude, point.Longitude));
                        featureCollection.Add(new Feature(pointGeometry, attributesTable));
                    }
                }
            }
            return featureCollection;
        }

		#endregion

        #region Route Operations

        /// <summary>
        /// Concatenates two routes.
        /// </summary>
        /// <param name="route1"></param>
        /// <param name="route2"></param>
        /// <returns></returns>
        public static Route Concatenate(Route route1, Route route2)
        {
            return Route.Concatenate(route1, route2, true);
        }

        /// <summary>
        /// Concatenates two routes.
        /// </summary>
        /// <param name="route1"></param>
        /// <param name="route2"></param>
        /// <param name="clone"></param>
        /// <returns></returns>
        public static Route Concatenate(Route route1, Route route2, bool clone)
        {
            if (route1 == null) return route2;
            if (route2 == null) return route1;
            if (route1.Segments.Length == 0) return route2;
            if (route2.Segments.Length == 0) return route1;
            var vehicle = route1.Vehicle;
            if (route1.Vehicle != route2.Vehicle)
            { // vehicles are different, is possible, vehicles also set in each segment.
                vehicle = null;
            }

            // get the end/start point.
            var end = route1.Segments[route1.Segments.Length - 1];
            var endTime = end.Time;
            var endDistance = end.Distance;
            var start = route2.Segments[0];

            // only do all this if the routes are 'concatenable'.
            if (end.Latitude == start.Latitude &&
                end.Longitude == start.Longitude)
            {
                // construct the new route.
                var route = new Route();

                // concatenate points.
                var entries = new List<RouteSegment>();
                for (var idx = 0; idx < route1.Segments.Length - 1; idx++)
                {
                    if (clone)
                    {
                        entries.Add(route1.Segments[idx].Clone() as RouteSegment);
                    }
                    else
                    {
                        entries.Add(route1.Segments[idx]);
                    }
                }

                // merge last and first entry.
                var mergedEntry = route1.Segments[route1.Segments.Length - 1].Clone() as RouteSegment;
                mergedEntry.Type = RouteSegmentType.Along;
                if (route2.Segments[0].Points != null && route2.Segments[0].Points.Length > 0)
                { // merge in important points from the second route too but do not keep duplicates.
                    var points = new List<RoutePoint>();
                    if (mergedEntry.Points != null)
                    { // keep originals.
                        points.AddRange(mergedEntry.Points);
                    }
                    for (int otherIdx = 0; otherIdx < route2.Segments[0].Points.Length; otherIdx++)
                    { // remove duplicates.
                        bool found = false;
                        for (int idx = 0; idx < points.Count; idx++)
                        {
                            if (points[idx].RepresentsSame(
                                route2.Segments[0].Points[otherIdx]))
                            { // the points represent the same info!
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        { // the point was not in there yet!
                            points.Add(route2.Segments[0].Points[otherIdx]);
                        }
                    }
                    mergedEntry.Points = points.ToArray();
                }
                entries.Add(mergedEntry);

                // add points of the next route.
                for (var idx = 1; idx < route2.Segments.Length; idx++)
                {
                    if (clone)
                    {
                        entries.Add(route2.Segments[idx].Clone() as RouteSegment);
                    }
                    else
                    {
                        entries.Add(route2.Segments[idx]);
                    }
                    entries[entries.Count - 1].Distance = entries[entries.Count - 1].Distance + endDistance;
                    entries[entries.Count - 1].Time = entries[entries.Count - 1].Time + endTime;
                }
                route.Segments = entries.ToArray();

                // concatenate tags.
                var tags = new List<RouteTags>();
                if (route1.Tags != null) { tags.AddRange(route1.Tags); }
                if (route2.Tags != null) { tags.AddRange(route2.Tags); }
                route.Tags = tags.ToArray();

                // set the vehicle.
                route.Vehicle = vehicle;
                return route;
            }
            else
            {
                throw new ArgumentOutOfRangeException("Contatenation routes can only be done when the end point of the first route equals the start of the second.");
            }
        }

        #endregion

        #region Metrics and Calculations

        /// <summary>
        /// The distance in meter.
        /// </summary>
        /// <remarks>Distance should always be set.</remarks>
        public double TotalDistance { get; set; }

        /// <summary>
        /// The time in seconds.
        /// </summary>
        public double TotalTime { get; set; }

        /// <summary>
        /// The times have been set.
        /// </summary>
        public bool HasTimes { get; set; }

        /// <summary>
        /// Returns the bounding box around this route.
        /// </summary>
        /// <returns></returns>
        public GeoCoordinateBox GetBox()
        {
            return new GeoCoordinateBox(this.GetPoints().ToArray());
        }

        /// <summary>
        /// Returns the points along the route for the entire route in the correct order.
        /// </summary>
        /// <returns></returns>
        public List<GeoCoordinate> GetPoints()
        {
            var coordinates = new List<GeoCoordinate>(this.Segments.Length);
            for (int p = 0; p < this.Segments.Length; p++)
            {
                coordinates.Add(new GeoCoordinate(this.Segments[p].Latitude, this.Segments[p].Longitude));
            }
            return coordinates;
        }

        /// <summary>
        /// Calculates the position on the route after the given distance from the starting point.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public GeoCoordinate PositionAfter(Meter m)
        {
            var distanceMeter = 0.0;
            var points = this.GetPoints();
            for (int idx = 0; idx < points.Count - 1; idx++)
            {
                var currentDistance = points[idx].DistanceReal(points[idx + 1]).Value;
                if (distanceMeter + currentDistance >= m.Value)
                { // the current distance should be in this segment.
                    var segmentDistance = m.Value - distanceMeter;
                    var direction = points[idx + 1] - points[idx];
                    direction = direction * (segmentDistance / currentDistance);
                    var position = points[idx] + direction;
                    return new GeoCoordinate(position[1], position[0]);
                }
				distanceMeter += currentDistance;
            }
            return null;
        }

        /// <summary>
        /// Calculates the closest point on the route relative to the given coordinate.
        /// </summary>
        /// <param name="coordinates"></param>
        /// <param name="projectedCoordinates"></param>
        /// <returns></returns>
        public bool ProjectOn(GeoCoordinate coordinates, out GeoCoordinate projectedCoordinates)
        {
            int entryIdx;
            Meter distanceToProjected;
            Second timeToProjected;
            return this.ProjectOn(coordinates, out projectedCoordinates, out entryIdx, out distanceToProjected, out timeToProjected);
        }

        /// <summary>
        /// Calculates the closest point on the route relative to the given coordinate.
        /// </summary>
        /// <param name="coordinates"></param>
        /// <param name="projectedCoordinates"></param>
        /// <param name="distanceToProjected"></param>
        /// <param name="timeFromStart"></param>
        /// <returns></returns>
        public bool ProjectOn(GeoCoordinate coordinates, out GeoCoordinate projectedCoordinates, out Meter distanceToProjected, out Second timeFromStart)
        {
            int entryIdx;
            return this.ProjectOn(coordinates, out projectedCoordinates, out entryIdx, out distanceToProjected, out timeFromStart);
        }

        /// <summary>
        /// Calculates the closest point on the route relative to the given coordinate.
        /// </summary>
        /// <param name="coordinates"></param>
        /// <param name="distanceFromStart"></param>
        /// <returns></returns>
        public bool ProjectOn(GeoCoordinate coordinates, out Meter distanceFromStart)
        {
            int entryIdx;
            GeoCoordinate projectedCoordinates;
            Second timeFromStart;
            return this.ProjectOn(coordinates, out projectedCoordinates, out entryIdx, out distanceFromStart, out timeFromStart);
        }

        /// <summary>
        /// Calculates the closest point on the route relative to the given coordinate.
        /// </summary>
        /// <returns></returns>
        public bool ProjectOn(GeoCoordinate coordinates, out GeoCoordinate projectedCoordinates, out int entryIndex, out Meter distanceFromStart, out Second timeFromStart)
        {
            double distance = double.MaxValue;
            distanceFromStart = 0;
            timeFromStart = 0;
            double currentDistanceFromStart = 0;
            projectedCoordinates = null;
            entryIndex = -1;

            // loop over all points and try to project onto the line segments.
            GeoCoordinate projected;
            double currentDistance;
            var points = this.GetPoints();
            for (int idx = 0; idx < points.Count - 1; idx++)
            {
                var line = new GeoCoordinateLine(points[idx], points[idx + 1], true, true);
                var projectedPoint = line.ProjectOn(coordinates);
                if (projectedPoint != null)
                { // there was a projected point.
                    projected = new GeoCoordinate(projectedPoint[1], projectedPoint[0]);
                    currentDistance = coordinates.Distance(projected);
                    if (currentDistance < distance)
                    { // this point is closer.
                        projectedCoordinates = projected;
                        entryIndex = idx;
                        distance = currentDistance;

                        // calculate distance/time.
                        double localDistance = projected.DistanceReal(points[idx]).Value;
                        distanceFromStart = currentDistanceFromStart + localDistance;
                        if(this.HasTimes && idx > 0)
                        { // there should be proper timing information.
                            double timeToSegment = this.Segments[idx].Time;
                            double timeToNextSegment = this.Segments[idx + 1].Time;
                            timeFromStart = timeToSegment + ((timeToNextSegment - timeToSegment) * (localDistance / line.LengthReal.Value));
                        }
                    }
                }

                // check first point.
                projected = points[idx];
                currentDistance = coordinates.Distance(projected);
                if (currentDistance < distance)
                { // this point is closer.
                    projectedCoordinates = projected;
                    entryIndex = idx;
                    distance = currentDistance;
                    distanceFromStart = currentDistanceFromStart;
                    if (this.HasTimes)
                    { // there should be proper timing information.
                        timeFromStart = this.Segments[idx].Time;
                    }
                }
                
                // update distance from start.
                currentDistanceFromStart = currentDistanceFromStart + points[idx].DistanceReal(points[idx + 1]).Value;
            }

            // check last point.
            projected = points[points.Count - 1];
            currentDistance = coordinates.Distance(projected);
            if (currentDistance < distance)
            { // this point is closer.
                projectedCoordinates = projected;
                entryIndex = points.Count - 1;
                distance = currentDistance;
                distanceFromStart = currentDistanceFromStart;
                if (this.HasTimes)
                { // there should be proper timing information.
                    timeFromStart = this.Segments[points.Count - 1].Time;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns an enumerable of route positions with the given interval between them.
        /// </summary>
        /// <param name="interval"></param>
        /// <returns></returns>
        public IEnumerable<GeoCoordinate> GetRouteEnumerable(Meter interval)
        {
            return new RouteEnumerable(this, interval);
        }

        #endregion
    }

    /// <summary>
    /// Structure representing one point in a route that has been routed to.
    /// </summary>
    public class RoutePoint : ICloneable
    {
        /// <summary>
        /// The name of the point.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The latitude of the entry.
        /// </summary>
        public float Latitude { get; set; }

        /// <summary>
        /// The longitude of the entry.
        /// </summary>
        public float Longitude { get; set; }
        
        /// <summary>
        /// Tags for this route point.
        /// </summary>
        public RouteTags[] Tags { get; set; }

        /// <summary>
        /// A number of route metrics, usually containing time/distance.
        /// </summary>
        /// <remarks>Can also be use for CO2 calculations or quality estimates.</remarks>
        public RouteMetric[] Metrics { get; set; }

        #region ICloneable Members

        /// <summary>
        /// Clones this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            var clone = new RoutePoint();
            clone.Latitude = this.Latitude;
            clone.Longitude = this.Longitude;
            if (this.Metrics != null)
            {
                clone.Metrics = new RouteMetric[this.Metrics.Length];
                for (int idx = 0; idx < this.Metrics.Length; idx++)
                {
                    clone.Metrics[idx] = this.Metrics[idx].Clone() as RouteMetric;
                }
            }
            clone.Name = this.Name;
            if (this.Tags != null)
            {
                clone.Tags = new RouteTags[this.Tags.Length];
                for (int idx = 0; idx < this.Tags.Length; idx++)
                {
                    clone.Tags[idx] = this.Tags[idx].Clone() as RouteTags;
                }
            }
            return clone;            
        }

        #endregion

        /// <summary>
        /// Returns true if the given point has the same name tags and positiong.
        /// </summary>
        /// <param name="routePoint"></param>
        /// <returns></returns>
        internal bool RepresentsSame(RoutePoint routePoint)
        {
            if (routePoint == null) return false;

            if (this.Longitude == routePoint.Longitude &&
                this.Latitude == routePoint.Latitude &&
                this.Name == routePoint.Name)
            {
                if (routePoint.Tags != null || routePoint.Tags.Length == 0)
                { // there are tags in the other point.
                    if (this.Tags != null || this.Tags.Length == 0)
                    { // there are also tags in this point.
                        if (this.Tags.Length == routePoint.Tags.Length)
                        { // and they have the same number of tags!
                            for (int idx = 0; idx < this.Tags.Length; idx++)
                            {
                                if (this.Tags[idx].Key != routePoint.Tags[idx].Key ||
                                    this.Tags[idx].Value != routePoint.Tags[idx].Value)
                                { // tags don't equal.
                                    return false;
                                }
                            }
                            return true;
                        }
                        return false;
                    }
                }
                return (this.Tags != null || this.Tags.Length == 0);
            }
            return false;
        }
    }

    /// <summary>
    /// Represents a point and the previous segment of the route.
    /// </summary>
    public class RouteSegment : ICloneable
    {
        /// <summary>
        /// The type of this entry.
        /// Start: Has no way from, distance from, angle or angles on poi's.
        /// Along: Has all data.
        /// Stop: Has all data but is the end point.
        /// </summary>
        public RouteSegmentType Type { get; set; }

        /// <summary>
        /// The latitude of the entry.
        /// </summary>
        public float Latitude { get; set; }

        /// <summary>
        /// The longitude of the entry.
        /// </summary>
        public float Longitude { get; set; }

        /// <summary>
        /// The name of the vehicle that this entry was calculated for.
        /// </summary>
        /// <remarks>This vehicle name is empty for unimodal routes.</remarks>
        public string Vehicle { get; set; }

        /// <summary>
        /// Tags of this entry.
        /// </summary>
        public RouteTags[] Tags { get; set; }

        /// <summary>
        /// A number of route metrics, usually containing time/distance.
        /// </summary>
        /// <remarks>Can also be use for CO2 calculations or quality estimates.</remarks>
        public RouteMetric[] Metrics { get; set; }

        /// <summary>
        /// Distance in meter to reach this part of the route.
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// Estimated time in seconds to reach this part of the route.
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// The important or relevant points for this route at this point.
        /// </summary>
        public RoutePoint[] Points { get; set; }

        /// <summary>
        /// The name of the way the route comes from.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// All the names of the ways indexed according to the alpha-2 code of ISO 639-1.
        /// </summary>
        public RouteTags[] Names { get; set; }

        /// <summary>
        /// The side streets entries.
        /// </summary>
        public RouteSegmentBranch[] SideStreets { get; set; }

        #region ICloneable Members

        /// <summary>
        /// Clones this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            var clone = new RouteSegment();
            clone.Distance = this.Distance;
            clone.Latitude = this.Latitude;
            clone.Longitude = this.Longitude;
            if (this.Metrics != null)
            {
                clone.Metrics = new RouteMetric[this.Metrics.Length];
                for (int idx = 0; idx < this.Metrics.Length; idx++)
                {
                    clone.Metrics[idx] = this.Metrics[idx].Clone() as RouteMetric;
                }
            }
            if (this.Points != null)
            {
                clone.Points = new RoutePoint[this.Points.Length];
                for (int idx = 0; idx < this.Points.Length; idx++)
                {
                    clone.Points[idx] = this.Points[idx].Clone() as RoutePoint;
                }
            }
            if (this.SideStreets != null)
            {
                clone.SideStreets = new RouteSegmentBranch[this.SideStreets.Length];
                for (int idx = 0; idx < this.SideStreets.Length; idx++)
                {
                    clone.SideStreets[idx] = this.SideStreets[idx].Clone() as RouteSegmentBranch;
                }
            }
            if (this.Tags != null)
            {
                clone.Tags = new RouteTags[this.Tags.Length];
                for (int idx = 0; idx < this.Tags.Length; idx++)
                {
                    clone.Tags[idx] = this.Tags[idx].Clone() as RouteTags;
                }
            }
            clone.Time = this.Time;
            clone.Type = this.Type;
            clone.Name = this.Name;
            if (this.Names != null)
            {
                clone.Names = new RouteTags[this.Names.Length];
                for (int idx = 0; idx < this.Names.Length; idx++)
                {
                    clone.Names[idx] = this.Names[idx].Clone() as RouteTags;
                }
            }
            clone.Vehicle = this.Vehicle;
            return clone;
        }

        #endregion

        /// <summary>
        /// Returns a string representation of this segment.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if(this.Vehicle != null && this.Name != null)
            {
                return string.Format("Segment: {0} for {1} @{2}s {3}m",
                    this.Name, this.Vehicle, this.Time, this.Distance);
            }
            else if(this.Vehicle != null)
            {
                return string.Format("Segment: for {0} @{1}s {2}m",
                    this.Vehicle, this.Time, this.Distance);
            }
            else if (this.Name != null)
            {
                return string.Format("Segment: {0} @{1}s {2}m",
                    this.Name, this.Time, this.Distance);
            }
            return string.Format("Segment: @{0}s {1}m",
                this.Time, this.Distance);
        }
    }

    /// <summary>
    /// Represents a type of point entry.
    /// </summary>
    public enum RouteSegmentType
    {
        /// <summary>
        /// Start type.
        /// </summary>
        Start,
        /// <summary>
        /// Along type.
        /// </summary>
        Along,
        /// <summary>
        /// Stop type.
        /// </summary>
        Stop
    }

    /// <summary>
    /// Represents a segment that has not been taken but is important to the route.
    /// </summary>
    public class RouteSegmentBranch : ICloneable
    {
        /// <summary>
        /// The latitude of the entry.
        /// </summary>
        public float Latitude { get; set; }

        /// <summary>
        /// The longitude of the entry.
        /// </summary>
        public float Longitude { get; set; }

        /// <summary>
        /// Tags of this entry.
        /// </summary>
        public RouteTags[] Tags { get; set; }

        /// <summary>
        /// The name of the way the route comes from.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// All the names of the ways indexed according to the alpha-2 code of ISO 639-1.
        /// </summary>
        public RouteTags[] Names { get; set; }

        #region ICloneable Members

        /// <summary>
        /// Returns a clone of this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            RouteSegmentBranch clone = new RouteSegmentBranch();
            clone.Latitude = this.Latitude;
            clone.Longitude = this.Longitude;
            if (this.Tags != null)
            {
                clone.Tags = new RouteTags[this.Tags.Length];
                for (int idx = 0; idx < this.Tags.Length; idx++)
                {
                    clone.Tags[idx] = this.Tags[idx].Clone() as RouteTags;
                }
            }
            clone.Name = this.Name;
            if (this.Names != null)
            {
                clone.Names = new RouteTags[this.Names.Length];
                for (int idx = 0; idx < this.Names.Length; idx++)
                {
                    clone.Names[idx] = this.Names[idx].Clone() as RouteTags;
                }
            }
            return clone;
        }

        #endregion
    }

    /// <summary>
    /// Represents a key value pair.
    /// </summary>
    public class RouteTags : ICloneable
    {
        /// <summary>
        /// The key.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The value.
        /// </summary>
        public string Value { get; set; }

        #region ICloneable Members

        /// <summary>
        /// Returns a clone of this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            var clone = new RouteTags();
            clone.Key = this.Key;
            clone.Value = this.Value;
            return clone;
        }

        #endregion

        /// <summary>
        /// Returns a System.String that represents the current System.Object.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{0}={1}",
                this.Key, this.Value);
        }
    }

    /// <summary>
    /// Contains extensions for route tags.
    /// </summary>
    public static class RouteTagsExtensions
    {        
        /// <summary>
        /// Converts a dictionary of tags to a RouteTags array.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static RouteTags[] ConvertFrom(this TagsCollectionBase tags)
        {
            var tagsList = new List<RouteTags>();
            foreach (Tag pair in tags)
            {
                var tag = new RouteTags();
                tag.Key = pair.Key;
                tag.Value = pair.Value;
                tagsList.Add(tag);
            }
            return tagsList.ToArray();
        }

        /// <summary>
        /// Converts a RouteTags array to a list of KeyValuePairs.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static TagsCollectionBase ConvertToTagsCollection(this RouteTags[] tags)
        {
            var tagsList = new TagsCollection();
            if (tags != null)
            {
                foreach (var pair in tags)
                {
                    tagsList.Add(new Tag(pair.Key, pair.Value));
                }
            }
            return tagsList;
        }

        /// <summary>
        /// Converts a dictionary of tags to a RouteTags array.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static RouteTags[] ConvertFrom(this IDictionary<string, string> tags)
        {
            var tags_list = new List<RouteTags>();
            foreach (var pair in tags)
            {
                RouteTags tag = new RouteTags();
                tag.Key = pair.Key;
                tag.Value = pair.Value;
                tags_list.Add(tag);
            }
            return tags_list.ToArray();
        }

        /// <summary>
        /// Converts a list of KeyValuePairs to a RouteTags array.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static RouteTags[] ConvertFrom(this List<KeyValuePair<string, string>> tags)
        {
            var tagsList = new List<RouteTags>();
            if (tags != null)
            {
                foreach (var pair in tags)
                {
                    var tag = new RouteTags();
                    tag.Key = pair.Key;
                    tag.Value = pair.Value;
                    tagsList.Add(tag);
                }
            }
            return tagsList.ToArray();
        }

        /// <summary>
        /// Converts a RouteTags array to a list of KeyValuePairs.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static List<KeyValuePair<string, string>> ConvertTo(this RouteTags[] tags)
        {
            var tagsList = new List<KeyValuePair<string, string>>();
            if (tags != null)
            {
                foreach (RouteTags pair in tags)
                {
                    tagsList.Add(new KeyValuePair<string, string>(pair.Key, pair.Value));
                }
            }
            return tagsList;
        }

        /// <summary>
        /// Returns the value of the first tag with the key given.
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetValueFirst(this RouteTags[] tags, string key)
        {
            string first_value = null;
            if (tags != null)
            {
                foreach (RouteTags tag in tags)
                {
                    if (tag.Key == key)
                    {
                        first_value = tag.Value;
                        break;
                    }
                }
            }
            return first_value;
        }

        /// <summary>
        /// Returns all values for a given key.
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static List<string> GetValues(this RouteTags[] tags, string key)
        {
            List<string> values = new List<string>();
            if (tags != null)
            {
                foreach (RouteTags tag in tags)
                {
                    if (tag.Key == key)
                    {
                        values.Add(tag.Value);
                    }
                }
            }
            return values;
        }
    }

    /// <summary>
    /// Represents a key value pair.
    /// </summary>
    public class RouteMetric : ICloneable
    {
        /// <summary>
        /// The key.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The value.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Convert from a regular tag dictionary.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static RouteMetric[] ConvertFrom(IDictionary<string, double> tags)
        {
            var tagsList = new List<RouteMetric>();
            foreach (KeyValuePair<string, double> pair in tags)
            {
                RouteMetric tag = new RouteMetric();
                tag.Key = pair.Key;
                tag.Value = pair.Value;
                tagsList.Add(tag);
            }
            return tagsList.ToArray();
        }

        /// <summary>
        /// Converts to regular tags list.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static List<KeyValuePair<string, double>> ConvertTo(RouteMetric[] tags)
        {
            var tagsList = new List<KeyValuePair<string, double>>();
            if (tags != null)
            {
                foreach (RouteMetric pair in tags)
                {
                    tagsList.Add(new KeyValuePair<string, double>(pair.Key, pair.Value));
                }
            }
            return tagsList;
        }

        #region ICloneable Members

        /// <summary>
        /// Returns a clone of this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            RouteMetric clone = new RouteMetric();
            clone.Key = this.Key;
            clone.Value = this.Value;
            return clone;
        }

        #endregion
    }
}