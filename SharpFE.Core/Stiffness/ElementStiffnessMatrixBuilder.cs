﻿//-----------------------------------------------------------------------
// <copyright file="ElementStiffnessMatrixBuilder.cs" company="SharpFE">
//     Copyright Iain Sproat, 2012 - 2013.
// </copyright>
//-----------------------------------------------------------------------

namespace SharpFE.Stiffness
{
    using System;
    using System.Collections.Generic;
    using SharpFE;
    
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ElementStiffnessMatrixBuilder<T> : IElementStiffnessCalculator
        where T : FiniteElement
    {
        /// <summary>
        /// The global stiffness matrix of this element
        /// </summary>
        private StiffnessMatrix globStiffMat;
        
        /// <summary>
        /// 
        /// </summary>
        private int elementStateAtWhichGlobalStiffnessMatrixWasLastBuilt;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ElementStiffnessMatrixBuilder">ElementStiffnessMatrixBuilder</see> class.
        /// </summary>
        /// <param name="finiteElement"></param>
        protected ElementStiffnessMatrixBuilder(T finiteElement)
        {
            Guard.AgainstNullArgument(finiteElement, "finiteElement");
            this.Element = finiteElement;
            
            this.HasBeenInitialized = true;
        }
        
        /// <summary>
        /// 
        /// </summary>
        public T Element
        {
            get;
            private set;
        }
        
        /// <summary>
        /// Gets the stiffness matrix of this element as rotated to global coordinates.
        /// </summary>
        public StiffnessMatrix StiffnessMatrixInGlobalCoordinates
        {
            get
            {
                if (this.Element.IsDirty(this.elementStateAtWhichGlobalStiffnessMatrixWasLastBuilt))
                {
                    this.BuildGlobalStiffnessMatrix();
                    this.elementStateAtWhichGlobalStiffnessMatrixWasLastBuilt = this.Element.GetHashCode();
                }
                
                return this.globStiffMat;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        protected bool HasBeenInitialized
        {
            get;
            private set;
        }           
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public abstract KeyedRowColumnMatrix<DegreeOfFreedom, NodalDegreeOfFreedom> ShapeFunctionVector(FiniteElementNode locationInGlobalCoordinates);
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract KeyedRowColumnMatrix<Strain, NodalDegreeOfFreedom> StrainDisplacementMatrix(FiniteElementNode locationInGlobalCoordinates);
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract StiffnessMatrix LocalStiffnessMatrix();
        
        /// <summary>
        /// Gets the exact stiffness value for a given node and degree of freedom combinations.
        /// </summary>
        /// <param name="rowNode">The node defining the row (force equations)</param>
        /// <param name="rowDegreeOfFreedom">The degree of freedom defining the row (force equations)</param>
        /// <param name="columnNode">The node defining the column (displacement equations)</param>
        /// <param name="columnDegreeOfFreedom">the degree of freedom defining the column (displacement equations)</param>
        /// <returns>A value representing the stiffness at the given locations</returns>
        /// <exception cref="ArgumentException">Thrown if either of the nodes is not part of this element, or either of the degrees of freedom are not supported by this element.</exception>
        public double GetStiffnessInGlobalCoordinatesAt(IFiniteElementNode rowNode, DegreeOfFreedom rowDegreeOfFreedom, IFiniteElementNode columnNode, DegreeOfFreedom columnDegreeOfFreedom)
        {
            if (rowNode == null)
            {
                throw new ArgumentNullException("rowNode");
            }
            
            if (columnNode == null)
            {
                throw new ArgumentNullException("columnNode");
            }
            
            return this.StiffnessMatrixInGlobalCoordinates.At(rowNode, rowDegreeOfFreedom, columnNode, columnDegreeOfFreedom);
        }
        
        
        
        /// <summary>
        /// 
        /// </summary>
        protected void ThrowIfNotInitialized()
        {
            if (!this.HasBeenInitialized)
            {
                throw new InvalidOperationException("This StiffnessMatrixBuilder has not been initialized correctly.  The Initialize(FiniteElement) method should be called first.");
            }
        }
        

        
        /// <summary>
        /// Prepares and generates the stiffness matrix.
        /// It creates an entirely new matrix from the current set of nodes and the supported degrees of freedom of this element.
        /// It calls the GenerateStiffnessMatrix method which inheriting classes are expected to implement.
        /// It sets the stiffnessMatrixHasBeenGenerated flag to true.
        /// </summary>
        private void BuildGlobalStiffnessMatrix()
        {
            this.ThrowIfNotInitialized();
            
            StiffnessMatrix k = this.LocalStiffnessMatrix();
            if (k.Determinant() != 0)
            {
                throw new InvalidOperationException(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "The stiffness matrix for an individual element should be singular and non-invertible. i.e. it should have a zero determinant.  This is not the case for element {0} of type {1}",
                    this.Element,
                    this.Element.GetType()));
            }
            
            KeyedSquareMatrix<NodalDegreeOfFreedom> t = this.BuildStiffnessRotationMatrixFromLocalToGlobalCoordinates();
            
            ////FIXME these multiplications assume the keys of both matrices are ordered and identical
            KeyedSquareMatrix<NodalDegreeOfFreedom> kt = k.Multiply(t); // K*T
            KeyedSquareMatrix<NodalDegreeOfFreedom> ttransposed = t.Transpose(); // T^
            KeyedSquareMatrix<NodalDegreeOfFreedom> ttransposedkt = ttransposed.Multiply(kt); // (T^)*K*T
            this.globStiffMat = new StiffnessMatrix(ttransposedkt);
            
            if (this.globStiffMat.Determinant() != 0)
            {
                throw new InvalidOperationException(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "The global stiffness matrix for an individual element should be singular and non-invertible. i.e. it should have a zero determinant.  This is not the case for element {0} of type {1}",
                    this.Element,
                    this.Element.GetType()));
            }
        }
        
        /// <summary>
        /// Builds the rotational matrix from local coordinates to global coordinates.
        /// </summary>
        /// <returns></returns>
        private KeyedSquareMatrix<NodalDegreeOfFreedom> BuildStiffnessRotationMatrixFromLocalToGlobalCoordinates()
        {
            this.ThrowIfNotInitialized();
            
            KeyedSquareMatrix<DegreeOfFreedom> rotationMatrix = this.Element.CalculateElementRotationMatrix();
            
            KeyedSquareMatrix<NodalDegreeOfFreedom> elementRotationMatrixFromLocalToGlobalCoordinates = new KeyedSquareMatrix<NodalDegreeOfFreedom>(this.Element.SupportedNodalDegreeOfFreedoms);

            foreach (FiniteElementNode node in this.Element.Nodes)
            {
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.X), new NodalDegreeOfFreedom(node, DegreeOfFreedom.X), rotationMatrix.At(DegreeOfFreedom.X, DegreeOfFreedom.X));
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.X), new NodalDegreeOfFreedom(node, DegreeOfFreedom.Y), rotationMatrix.At(DegreeOfFreedom.X, DegreeOfFreedom.Y));
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.X), new NodalDegreeOfFreedom(node, DegreeOfFreedom.Z), rotationMatrix.At(DegreeOfFreedom.X, DegreeOfFreedom.Z));
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.Y), new NodalDegreeOfFreedom(node, DegreeOfFreedom.X), rotationMatrix.At(DegreeOfFreedom.Y, DegreeOfFreedom.X));
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.Y), new NodalDegreeOfFreedom(node, DegreeOfFreedom.Y), rotationMatrix.At(DegreeOfFreedom.Y, DegreeOfFreedom.Y));
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.Y), new NodalDegreeOfFreedom(node, DegreeOfFreedom.Z), rotationMatrix.At(DegreeOfFreedom.Y, DegreeOfFreedom.Z));
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.Z), new NodalDegreeOfFreedom(node, DegreeOfFreedom.X), rotationMatrix.At(DegreeOfFreedom.Z, DegreeOfFreedom.X));
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.Z), new NodalDegreeOfFreedom(node, DegreeOfFreedom.Y), rotationMatrix.At(DegreeOfFreedom.Z, DegreeOfFreedom.Y));
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.Z), new NodalDegreeOfFreedom(node, DegreeOfFreedom.Z), rotationMatrix.At(DegreeOfFreedom.Z, DegreeOfFreedom.Z));
                
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.XX), new NodalDegreeOfFreedom(node, DegreeOfFreedom.XX), 1.0);
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.YY), new NodalDegreeOfFreedom(node, DegreeOfFreedom.YY), 1.0);
                elementRotationMatrixFromLocalToGlobalCoordinates.At(new NodalDegreeOfFreedom(node, DegreeOfFreedom.ZZ), new NodalDegreeOfFreedom(node, DegreeOfFreedom.ZZ), 1.0);
            }
            
            return elementRotationMatrixFromLocalToGlobalCoordinates;
        }
    }
}
