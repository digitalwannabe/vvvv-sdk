// this is derived from the CSGTree tutorial by Alec Jacobson, which is part of libigl http://libigl.github.io/libigl/

// new code by digitalWannabe, lichterloh.tv, 2016
// all my comments will be marked with dW
// my changes basically are


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

#include <igl/copyleft/cgal/remesh_self_intersections.h>

#include <Eigen/Geometry>
#include <Eigen/StdVector>
#include <vector>
#include <algorithm>
#include <iostream>
#include <igl/copyleft/cgal/CSGTree.h>
#include <Eigen/Core>
#include <boost/algorithm/string/split.hpp>
#include <boost/algorithm/string/classification.hpp>
#include <CGAL/Exact_predicates_exact_constructions_kernel.h>
#include <CGAL/number_utils.h>

#include <igl/MeshBooleanType.h>
#include <igl/copyleft/cgal/string_to_mesh_boolean_type.h>
#include <igl/copyleft/cgal/mesh_boolean.h>



#include <string>
#include <vector>

#include <igl/readOFF.h>
//#include "tutorial_shared_path.h"


#include "main.h"

namespace BooleanMesh {


	using namespace Eigen;
	using namespace std;
	using namespace igl::copyleft::cgal;
	using namespace boost::algorithm;


	//dW: simple helper functions to fill eigen matrix/vector with the values from the external call

	MatrixXd FillMatrix(double Arr[], int nRows, int nCols) {
		MatrixXd Md(nRows, nCols);
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


	Eigen::MatrixXd VA, VB, VC;
	Eigen::VectorXi J, I;
	Eigen::MatrixXi FA, FB, FC;

	
	//dW: this is the main function to compute the weight matrix
	int* meshBoolean(char * operationString, double *VerticesA, int *IndicesA, double *VerticesB, int *IndicesB, int * binSizes) {//, int *PEext) {
		
			VA = FillMatrix(VerticesA, binSizes[0], 3);
			FA = FillMatrix(IndicesA, binSizes[1]/3, 3);

			VB = FillMatrix(VerticesB, binSizes[2], 3);
			FB = FillMatrix(IndicesB, binSizes[3]/3, 3);

			igl::MeshBooleanType boolean_type = string_to_mesh_boolean_type(operationString);

			mesh_boolean(VA, FA, VB, FB, boolean_type, VC, FC, J);

			int* resultArray = new int[2];
			resultArray[0] = VC.size()/3;
			resultArray[1] = FC.size();

			return resultArray;
			delete[] resultArray;
		
		}

	void getValues(double * verticesOut, int * indicesOut) {

		
//		verticesOut = new double[VC.size()];
		for (int i = 0; i<VC.rows(); i++)
		{
			for (int j = 0; j<VC.cols(); j++)
			{
				verticesOut[i * 3 + j] = VC(i, j);
			}
		}
		/*		for (int i = 0; i<FC.rows(); i++)
		{
			for (int j = 0; j<FC.cols(); j++)
			{
				indicesOut[i * 3 + j] = FC(i, j);
			}
		}
		*/


//		Map<Matrix<int, -1, -1, RowMajor>>(verticesOut, VC.rows(), VC.cols()) = VC;

//		indicesOut = new int[FC.size()];
		//dW: set array pointer with values from matrix
		Map<Matrix<int, -1, -1, RowMajor>>(indicesOut, FC.rows(), FC.cols()) = FC;

		/*			bFacesOut = new int[m_J.size()];
		//dW: set array pointer with values from matrix
		Map<Matrix<int, -1, -1, RowMajor>>(indicesOut, m_J.rows(), m_J.cols()) = m_J;*/
	}
}