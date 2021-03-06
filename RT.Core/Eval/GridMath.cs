﻿using RT.Core.Geometry;
using RT.Core.Utilities.RTMath;
using System;
using System.Collections.Generic;
using System.Text;

namespace RT.Core.Eval
{
    public class GridMath
    {
        public GammaDistribution Gamma(IVoxelDataStructure reference, IVoxelDataStructure evaluated, IProgress<int> progress, float distTol, float doseTol, float threshold)
        {
            var gammaDistribution = new GammaDistribution();
            VectorField gammaVectorField = new VectorField();
            var xVectors = CreateBlank(reference);
            var yVectors = CreateBlank(reference);
            var zVectors = CreateBlank(reference);

            float thresholdDose = (threshold / 100) * reference.MaxVoxel.Value * reference.Scaling;

            doseTol = (doseTol / 100) * (reference.MaxVoxel.Value * reference.Scaling); //global gamma

            var newGrid = CreateBlank(reference);
            //Create a sorted list of offsets with a certain diameter and step size
            var offsets = CreateOffsets(distTol * 3, distTol / 10.0, newGrid.GridSpacing);

            Point3d posn = new Point3d();
            Point3d posn2 = new Point3d();

            double dx = 0, dy = 0, dz = 0; // Keep track of where we are pointing.

            int voxelNum = 0;
            int totalVoxels = newGrid.NumberOfVoxels;
            foreach (Voxel voxel in newGrid)
            {
                voxelNum++;
                if (voxelNum % (totalVoxels / 20) == 0)
                    progress.Report((int)(100 * ((double)voxelNum / (double)totalVoxels)));

                posn = voxel.Position;

                var refDose = reference.Interpolate(posn).Value * reference.Scaling;
                var evalDose = evaluated.Interpolate(posn).Value * evaluated.Scaling;
                if (refDose < thresholdDose && evalDose < thresholdDose)
                {
                    newGrid.SetVoxelByCoords((float)posn.X, (float)posn.Y, (float)posn.Z, -1);
                    continue;
                }

                //Set minGamma squared using a point with no offset (dose difference only).
                var dd = refDose - evalDose;

                float minGammaSquared = GammaSquared(dd * dd, 0, doseTol * doseTol, distTol * distTol);
                dx = dy = dz = 0;

                //Store the last distance we evaluated
                float lastDistSq = 0;

                //loop throught the sorted list of offsets
                for (int o = 1; o < offsets.Count; o++)
                {
                    Offset offset = offsets[o];
                    float distSq = (float)offset.DistanceSquared;

                    //set posn2 to to the actual physical location in the grid we are interested in
                    posn.Add(offset.Displacement, posn2);

                    if (minGammaSquared < distSq / (distTol * distTol) && distSq > lastDistSq)
                    {
                        break; //there is no way gamma can get smaller since distance is increasing in each offset and future gamma will be dominated by the DTA portion.
                    }

                    dx = offset.Displacement.X;
                    dy = offset.Displacement.Y;
                    dz = offset.Displacement.Z;

                    //compute dose difference squared and then gamma squared
                    refDose = reference.Interpolate(posn).Value * reference.Scaling;
                    evalDose = evaluated.Interpolate(posn2).Value * evaluated.Scaling;

                    float doseDiff = (refDose - evalDose);

                    float gammaSquared = GammaSquared(doseDiff * doseDiff, distSq, doseTol * doseTol, distTol * distTol);

                    if (gammaSquared < minGammaSquared)
                        minGammaSquared = gammaSquared;

                    lastDistSq = distSq;
                }

                float gamma = (float)Math.Sqrt((double)minGammaSquared);

                if(gamma >0 || gamma <0 || float.IsNaN(gamma) || float.IsInfinity(gamma) || float.IsNegativeInfinity(gamma))
                {

                }

                newGrid.SetVoxelByCoords((float)posn.X, (float)posn.Y, (float)posn.Z, gamma);
                xVectors.SetVoxelByCoords((float)posn.X, (float)posn.Y, (float)posn.Z, (float)dx);
                yVectors.SetVoxelByCoords((float)posn.X, (float)posn.Y, (float)posn.Z, (float)dy);
                zVectors.SetVoxelByCoords((float)posn.X, (float)posn.Y, (float)posn.Z, (float)dz);

                TestAndSetMinAndMax(newGrid, gamma);
            }

            gammaDistribution.Gamma = newGrid;
            gammaDistribution.Vectors.X = xVectors;
            gammaDistribution.Vectors.Y = yVectors;
            gammaDistribution.Vectors.Z = zVectors;
            //gammaDistribution.Jacobian = GetJacobianMatrix(gammaDistribution);

            return gammaDistribution;
        }

        private static void TestAndSetMinAndMax(GridBasedVoxelDataStructure grid, float gamma)
        {
            if (gamma > grid.MaxVoxel.Value)
                grid.MaxVoxel.Value = gamma;
            if (gamma < grid.MinVoxel.Value)
                grid.MinVoxel.Value = gamma;
        }

        private GridBasedVoxelDataStructure GetJacobianMatrix(GammaDistribution gammaDistribution)
        {
            var jacobian = CreateBlank(gammaDistribution.Gamma);
            var v = gammaDistribution.Vectors;
            foreach (Voxel voxel in jacobian)
            {
                jacobian.SetVoxelByCoords(
                    (float)voxel.Position.X,
                    (float)voxel.Position.Y,
                    (float)voxel.Position.Z,
                    (float)GetDeterminant(voxel.Position, v));
            }

            return jacobian;
        }

        private double GetDeterminant(Point3d p, VectorField f)
        {
            var dx = 1.0; var dy = dx; var dz = dx;
            var px = new Point3d(dx, 0, 0);
            var py = new Point3d(0, dy, 0);
            var pz = new Point3d(0, 0, dz);
            Matrix3d m = new Matrix3d();
            m.A00 = (f.X[p + px / 2] - f.X[p - px / 2]) / dx;
            m.A01 = (f.X[p + py / 2] - f.X[p - py / 2]) / dy;
            m.A02 = (f.X[p + pz / 2] - f.X[p - pz / 2]) / dz;

            m.A10 = (f.Y[p + px / 2] - f.X[p - px / 2]) / dx;
            m.A11 = (f.Y[p + py / 2] - f.X[p - py / 2]) / dy;
            m.A12 = (f.Y[p + pz / 2] - f.X[p - pz / 2]) / dz;

            m.A20 = (f.Z[p + px / 2] - f.X[p - px / 2]) / dx;
            m.A21 = (f.Z[p + py / 2] - f.X[p - py / 2]) / dy;
            m.A22 = (f.Z[p + pz / 2] - f.X[p - pz / 2]) / dz;

            return m.Determinate();
        }

        public List<Offset> CreateOffsets(double diameterMM, double stepMM, Point3d gridSizes)
        {
            List<Offset> offsets = new List<Offset>();
            //First surround with offset that correspond to grid points
            for(double i = -gridSizes.X*2; i<gridSizes.X*2;i++)
            {
                for(double j = -gridSizes.Y*2; j < gridSizes.Y * 2; j++)
                {
                    for(double k = -gridSizes.Z*2; k < gridSizes.Z; k++)
                    {
                        offsets.Add(new Offset() { Displacement = new Point3d(i, j, k) });
                    }
                }
            }

            offsets.Add(new Offset() { Displacement = new Point3d(0, 0, 0) });

            int n = (int)((diameterMM) / stepMM);
            if (n % 2 != 0)
                n++;
            var c0 = 0 - stepMM * (n/2); //ensure we go through 0
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    for (int k = 0; k < n; k++)
                    {
                        var xo = c0 + i * stepMM;
                        var yo = c0 + j * stepMM;
                        var zo = c0 + k * stepMM;
                        offsets.Add(new Offset() { Displacement = new Point3d(xo, yo, zo) });
                    }

            offsets.Sort();
            return offsets;
        }

        public float GammaSquared(float doseDiffSquared, float distSquared, float doseCriteriaSquared, float distCriteriaSquared)
        {
            return (doseDiffSquared / doseCriteriaSquared) + (distSquared / distCriteriaSquared);
        }


        public IVoxelDataStructure Subtract(IVoxelDataStructure grid1, IVoxelDataStructure grid2)
        {
            var newGrid = CreateBlank(grid1, grid2);
            int lenX = newGrid.XCoords.Length;
            int lenY = newGrid.YCoords.Length;
            int lenZ = newGrid.ZCoords.Length;
            Voxel v1 = new Voxel();
            Voxel v2 = new Voxel();
            for(int i = 0; i < lenX; i++)
            {
                for(int j = 0; j < lenY; j++)
                {
                    for(int k = 0; k < lenZ; k++)
                    {
                        double x = newGrid.XCoords[i];
                        double y = newGrid.YCoords[j];
                        double z = newGrid.ZCoords[k];
                        grid1.Interpolate(x, y, z, v1);
                        v1.Value *= grid1.Scaling;
                        grid2.Interpolate(x, y, z, v2);
                        v2.Value *= grid2.Scaling;
                        /*newGrid.Data[i, j, k] = 100 * (v1.Value - v2.Value) / (grid1.MaxVoxel.Value*grid1.Scaling);
                        if (newGrid.Data[i, j, k] > newGrid.MaxVoxel.Value)
                            newGrid.MaxVoxel.Value = newGrid.Data[i, j, k];
                        if (newGrid.Data[i, j, k] < newGrid.MinVoxel.Value)
                            newGrid.MinVoxel.Value = newGrid.Data[i, j, k];*/
                    }
                }
            }
            return newGrid;
        }
        
        /// <summary>
        /// Returns an empty grid with the same size as the grid
        /// </summary>
        /// <param name="grid"></param>
        /// <returns></returns>
        public GridBasedVoxelDataStructure CreateBlank(IVoxelDataStructure g)
        {
            var grid = (GridBasedVoxelDataStructure)g;
            var newGrid = new GridBasedVoxelDataStructure();
            newGrid.XRange = new Range(grid.XRange.Minimum, grid.XRange.Maximum);
            newGrid.YRange = new Range(grid.YRange.Minimum, grid.YRange.Maximum);
            newGrid.ZRange = new Range(grid.ZRange.Minimum, grid.ZRange.Maximum);
            newGrid.XCoords = new float[grid.XCoords.Length]; grid.XCoords.CopyTo(newGrid.XCoords, 0);
            newGrid.YCoords = new float[grid.YCoords.Length]; grid.YCoords.CopyTo(newGrid.YCoords, 0);
            newGrid.ZCoords = new float[grid.ZCoords.Length]; grid.ZCoords.CopyTo(newGrid.ZCoords, 0);
            newGrid.Scaling = 1;
            newGrid.Data = new float[newGrid.XCoords.Length * newGrid.YCoords.Length * newGrid.ZCoords.Length];
            newGrid.ConstantGridSpacing = grid.ConstantGridSpacing;
            newGrid.GridSpacing = new Point3d();
            grid.GridSpacing.CopyTo(newGrid.GridSpacing);
            return newGrid;
        }

        /// <summary>
        /// Returns a blank grid which encompasses both grid1 and grid2
        /// </summary>
        /// <param name="grid1"></param>
        /// <param name="grid2"></param>
        /// <returns></returns>
        public GridBasedVoxelDataStructure CreateBlank(IVoxelDataStructure grid1, IVoxelDataStructure grid2)
        {
            var grid = new GridBasedVoxelDataStructure();
            grid.XRange = grid1.XRange.Combine(grid2.XRange);
            grid.YRange = grid1.YRange.Combine(grid2.YRange);
            grid.ZRange = grid1.ZRange.Combine(grid2.ZRange);
            grid.ConstantGridSpacing = true;
            grid.GridSpacing = new Utilities.RTMath.Point3d(1, 1, 2);
            int lenX = (int)Math.Round((grid.XRange.Length / grid.GridSpacing.X))+1;
            int lenY = (int)Math.Round((grid.YRange.Length / grid.GridSpacing.Y))+1;
            int lenZ = (int)Math.Round((grid.ZRange.Length / grid.GridSpacing.Z))+1;
            grid.XCoords = new float[lenX];
            grid.YCoords = new float[lenY];
            grid.ZCoords = new float[lenZ];
            for (int i = 0; i < lenX; i++)
                grid.XCoords[i] = (float)(grid.XRange.Minimum + i * grid.GridSpacing.X);
            for (int j = 0; j < lenY; j++)
                grid.YCoords[j] = (float)(grid.YRange.Minimum + j * grid.GridSpacing.Y);
            for (int k = 0; k < lenZ; k++)
                grid.ZCoords[k] = (float)(grid.ZRange.Minimum + k * grid.GridSpacing.Z);

            grid.Data = new float[lenX * lenY * lenZ];
            return grid;
        }
    }
}
