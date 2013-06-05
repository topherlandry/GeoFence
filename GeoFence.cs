using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DotSpatial.Data;
using DotSpatial.Projections;
using DotSpatial.Topology;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using ProjNet.Converters;
using ProjNet.Converters.WellKnownText;

namespace GeoFence
{
    public class GeoFence
    {
        public static void Main(string[] args)
        {
            var nycBoroughs = Shapefile.OpenFile(@"C:\NYC Shape\nybb_13a\nybb.shp");
            var wktstring = "PROJCS[\"NAD_1983_StatePlane_New_York_Long_Island_FIPS_3104_Feet\",GEOGCS[\"GCS_North_American_1983\",DATUM[\"D_North_American_1983\",SPHEROID[\"GRS_1980\",6378137.0,298.257222101]],PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]],PROJECTION[\"Lambert_Conformal_Conic\"],PARAMETER[\"False_Easting\",984250.0],PARAMETER[\"False_Northing\",0.0],PARAMETER[\"Central_Meridian\",-74.0],PARAMETER[\"Standard_Parallel_1\",40.66666666666666],PARAMETER[\"Standard_Parallel_2\",41.03333333333333],PARAMETER[\"Latitude_Of_Origin\",40.16666666666666],UNIT[\"Foot_US\",0.3048006096012192]]";

            var latlongwkt = "GEOGCS [\"Longitude / Latitude (NAD 83)\",DATUM [\"NAD 83\",SPHEROID [\"GRS 80\",6378137,298.257222101]],PRIMEM [\"Greenwich\",0.000000],UNIT [\"Decimal Degree\",0.01745329251994330]]";
            var csvFile = @"C:\NYC Shape\studylatlong.csv";
            var targetFile = @"C:\NYC Shape\nycstudieslatlong.csv";

            var studyData = new List<StudyInfo>();

            using (StreamReader sr = new StreamReader(csvFile))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var tokens = line.Split(',');
                    var study = StudyInfo.CreateFromStrings(tokens);
                    if (study.Latitude < 40 || study.Latitude > 42
                         || study.Longitude < -75 || study.Longitude > -70)
                    { }
                    else
                        studyData.Add(StudyInfo.CreateFromStrings(tokens));
                }
                Console.WriteLine("# of Studies: " + studyData.Count());
            }

            ICoordinateSystem nycCS =
                CoordinateSystemWktReader.Parse(wktstring) as ICoordinateSystem;

            ICoordinateSystem baseCS =
                CoordinateSystemWktReader.Parse(latlongwkt) as ICoordinateSystem;

            var ctFactory = new CoordinateTransformationFactory();
            // Transform lat/long points into NYC UTM.
            var transformer = ctFactory.CreateFromCoordinateSystems(baseCS, nycCS);

            var nycStudies = new List<StudyInfo>();

            foreach (var study in studyData)
            {
                double[] fromPoint = { study.Longitude, study.Latitude };
                double[] toPoint = transformer.MathTransform.Transform(fromPoint);
                var studyLocation = new Coordinate(toPoint[0], toPoint[1]);
                if (IsPointInShape(studyLocation, nycBoroughs.Features))
                    nycStudies.Add(study);
            }

            Console.WriteLine("# of NYC Studies: " + nycStudies.Count());

            using (StreamWriter sw = new StreamWriter(targetFile))
            {
                foreach (var study in nycStudies)
                {
                    sw.Write("" + study.StudyID);
                    sw.Write(",");
                }
                sw.WriteLine();
            }

            Console.WriteLine("Output written.");
            Console.ReadLine();
        }

        private static bool IsPointInShape(Coordinate point, IFeatureList features)
        {
            foreach (var feature in features)
            {
                if (!PointInBoundingBox(point, feature.Coordinates))
                    continue;
                if (PointInPolygon(point, feature.Coordinates.ToArray()))
                    return true;
            }

            return false;
        }

        private static bool PointInBoundingBox(Coordinate point, IList<Coordinate> vertices)
        {
            double minX = vertices.Min(v => v.X);
            double maxX = vertices.Max(v => v.X);
            double minY = vertices.Min(v => v.Y);
            double maxY = vertices.Max(v => v.Y);

            double x = point.X;
            double y = point.Y;
            
            bool outsideBox = x > maxX || x < minX || y > maxY || y < minY;

            return !outsideBox;
        }

        private static bool PointInPolygon(Coordinate p, Coordinate[] poly)
        {
            Point p1, p2;

            bool inside = false;

            if (poly.Length < 3)
            {
                return inside;
            }

            Point oldPoint = new Point(
            poly[poly.Length - 1].X, poly[poly.Length - 1].Y);

            for (int i = 0; i < poly.Length; i++)
            {
                Point newPoint = new Point(poly[i].X, poly[i].Y);

                if (newPoint.X > oldPoint.X)
                {
                    p1 = oldPoint;
                    p2 = newPoint;
                }
                else
                {
                    p1 = newPoint;
                    p2 = oldPoint;
                }

                if ((newPoint.X < p.X) == (p.X <= oldPoint.X)
                && ((long)p.Y - (long)p1.Y) * (long)(p2.X - p1.X)
                 < ((long)p2.Y - (long)p1.Y) * (long)(p.X - p1.X))
                {
                    inside = !inside;
                }

                oldPoint = newPoint;
            }

            return inside;
        }
    }

    public class StudyInfo
    {
        public int StudyID { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public StudyInfo(int studyID, double latitude, double longitude)
        {
            StudyID = studyID;
            Latitude = latitude;
            Longitude = longitude;
        }

        public static StudyInfo CreateFromStrings(String[] tokens)
        {
            int studyID = Int32.Parse(tokens[0]);
            double latitude = Double.Parse(tokens[1]);
            double longitude = Double.Parse(tokens[2]);
            return new StudyInfo(studyID, latitude, longitude);
        }
    }
}