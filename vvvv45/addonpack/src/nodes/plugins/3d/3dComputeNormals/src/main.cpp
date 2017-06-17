// this is a modification of the BoundedBiharmonicWeights tutorial by Alec Jacobson, which is part of libigl http://libigl.github.io/libigl/

// modifications done by digitalWannabe, lichterloh.tv, 2016
// all my comments will be marked with dW
// my changes basically are
// -removed all rendering stuff
// -removed all source file loading (values to be passed by external calling function)
// -moved forward kinematics into a separate function
// -added matrix multiplication function for debugging
// =changed main functions' inputs/outputs and exported these to be able to call them from C# (ie vvvv)


// If you don't have mosek installed and don't want to install it. Then
// uncomment the following six lines.  Don't use static library for this
// example because of Mosek complications
//
#define IGL_NO_MOSEK
#ifdef IGL_NO_MOSEK
#ifdef IGL_STATIC_LIBRARY
#undef IGL_STATIC_LIBRARY
#endif
#endif
#include <igl/boundary_conditions.h>
//#include <igl/colon.h>
#include <igl/column_to_quats.h>
#include <igl/directed_edge_parents.h>
#include <igl/forward_kinematics.h>
//#include <igl/jet.h>
#include <igl/lbs_matrix.h>
//#include <igl/deform_skeleton.h>
#include <igl/normalize_row_sums.h>
#include <igl/readDMAT.h>
#include <igl/readMESH.h>
#include <igl/readTGF.h>
//#include <igl/viewer/Viewer.h>
#include <igl/bbw/bbw.h>
//#include <igl/embree/bone_heat.h>
#include <igl/copyleft/cgal/remesh_self_intersections.h>

#include <Eigen/Geometry>
#include <Eigen/StdVector>
#include <vector>
#include <algorithm>
#include <iostream>
#include <igl/per_vertex_normals.h>
#include <igl/per_face_normals.h>
#include <igl/per_corner_normals.h>


#include "main.h"

namespace ComputeN {


	using namespace Eigen;
	using namespace std;


	//dW: simple helper functions to fill eigen matrix/vector with the values from the external call

	MatrixXd FillMatrix( double Arr[], int nRows, int nCols) {
		MatrixXd Md(nRows,nCols);
		for (int i = 0; i < nRows; i++) {
			for (int j = 0; j < nCols; j++) {
				Md(i, j) = Arr[i*nCols + j];
			}
		}
		return Md;
	}

	MatrixXi FillMatrix(int Arr[], int nRows, int nCols) {
		MatrixXi Mi(nRows, nCols);
		for (int i = 0; i < nRows; i++) {
			for (int j = 0; j < nCols; j++) {
				Mi(i, j) = Arr[i*nCols + j];
			}
		}
		return Mi;
	}

	VectorXi FillVector(int Arr[], int nRows) {
		VectorXi Vi(nRows);
		for (int i = 0; i < nRows; i++) {
			Vi(i) = Arr[i];
		}
		return Vi;

	}




	//dW: this is the main function to compute the weight matrix
	void ComputeNormals(double *Vertices, int *TriangleI, double *faceN, double *vertexN, double *cornerN, double dihedral, unsigned int enumMode, int *binSizes, bool computeFace, bool computeVertex, bool computeCorner) {//, int *PEext) {
		

			Eigen::MatrixXi TriangleIndices;
			Eigen::MatrixXd VertexPoints, N_faces, N_vertices, N_corners;

			igl::PerVertexNormalsWeightingType Weighting;

			switch (enumMode)
			{
			case 0:
				Weighting = igl::PER_VERTEX_NORMALS_WEIGHTING_TYPE_UNIFORM;
				break;
			case 1:
				Weighting = igl::PER_VERTEX_NORMALS_WEIGHTING_TYPE_AREA;
				break;
			case 2:
				Weighting = igl::PER_VERTEX_NORMALS_WEIGHTING_TYPE_ANGLE;
				break;
			case 3:
				Weighting = igl::PER_VERTEX_NORMALS_WEIGHTING_TYPE_DEFAULT;
				break;
			case 4:
				Weighting = igl::NUM_PER_VERTEX_NORMALS_WEIGHTING_TYPE;
				break;
			default:
				Weighting = igl::PER_VERTEX_NORMALS_WEIGHTING_TYPE_DEFAULT;
			}
			

//			enum eMode = enumMode;

			//dW: assign names for readability
			int vertexCount = binSizes[0];
			int triangleCount = binSizes[1];		

			//dW: set matrices to values from vvvv
			VertexPoints = FillMatrix(Vertices, vertexCount, 3);
			TriangleIndices = FillMatrix(TriangleI, triangleCount, 3);


			// Compute per-face normals
			if (computeFace) {
				igl::per_face_normals(VertexPoints, TriangleIndices, N_faces);
				//dW: set array pointer with values from matrix
				Map<Matrix<double, -1, -1, RowMajor>>(faceN, N_faces.rows(), N_faces.cols()) = N_faces;
			}

			// Compute per-vertex normals
			if (computeVertex) {
				igl::per_vertex_normals(VertexPoints, TriangleIndices, Weighting, N_vertices);
				Map<Matrix<double, -1, -1, RowMajor>>(vertexN, N_vertices.rows(), N_vertices.cols()) = N_vertices;
			}

			// Compute per-corner normals, |dihedral angle| > 20 degrees --> crease
			if (computeCorner) {
				igl::per_corner_normals(VertexPoints, TriangleIndices, dihedral, N_corners);
				Map<Matrix<double, -1, -1, RowMajor>>(cornerN, N_corners.rows(), N_corners.cols()) = N_corners;
			}
			
		
		}
}