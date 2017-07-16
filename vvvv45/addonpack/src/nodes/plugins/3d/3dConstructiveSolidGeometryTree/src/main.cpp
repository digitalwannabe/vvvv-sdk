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

#include <string>
#include <vector>



#include "main.h"

namespace ComputeCSG {


	using namespace Eigen;
	using namespace std;
	using namespace igl::copyleft::cgal;
	using namespace boost::algorithm;

	
	typedef CGAL::Epeck::FT ExactScalar;
	typedef Eigen::MatrixXi POBF;
	typedef POBF::Index FIndex;
	typedef Eigen::Matrix<ExactScalar, Eigen::Dynamic, 3> MatrixX3E;
	

	vector<CSGTree> CSG_Stack;
	int *countVert;
	int *countInd;
	int *shiftVert;
	int *shiftInd;


	Eigen::MatrixXi TriangleIndices,TNull;
	Eigen::MatrixXd VertexPoints,VNull;

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

	const MatrixXd GetSubMatrix(MatrixXd Full, int start, int nRows, int nCols) {
		MatrixXd Md(nRows, nCols);
		for (int i = 0; i < nRows; i++) {
			for (int j = 0; j < nCols; j++) {
				Md(i, j) = Full(start + i, j);
			}
		}
		return Md;
	}

	const MatrixXi GetSubMatrix(MatrixXi Full, int start, int nRows, int nCols) {
		MatrixXi Mi(nRows, nCols);
		for (int i = 0; i < nRows; i++) {
			for (int j = 0; j < nCols; j++) {
				Mi(i, j) = Full(start + i, j);
			}
		}
		return Mi;
	}


	bool IsOperator(string s)
	{
		if (s == "union" || s == "minus" || s == "intersect" || s == "xor" || s == "resolve") {
			return true;
		} else{
			return false;
		}
	}

	bool isOperand(const std::string& s)
	{
		std::string::const_iterator it = s.begin();
		while (it != s.end() && std::isdigit(*it)) ++it;
		return !s.empty() && it == s.end();
	}

/*	bool IsOperand(string s)
	{
		if (is_number(s)) {
			return true;
		}
		else return false;
	}
*/

	void workThestack(string opElement) {

		if (!isOperand(opElement))
		{
			CSGTree  rightOperand;
			CSGTree leftOperand;
//			if (!CSG_Stack.empty()) {
				rightOperand = CSG_Stack.back();

				CSG_Stack.pop_back();
//			}
//			
//			if (!CSG_Stack.empty()) {
				leftOperand = CSG_Stack.back();

				CSG_Stack.pop_back();
//			}
			const CSGTree  rightOperandConst = rightOperand;
			const CSGTree  leftOperandConst = leftOperand;
//			}
/*
			const MatrixX3E VO = rightOperandConst.V();
			const MatrixXi IO = rightOperandConst.F();

			const CSGTree rO = { VO,IO };
//			const CSGTree lO

			MatrixX3E VO2 = leftOperand.V();
			POBF IO2;// = leftOperand.F();

			const CSGTree rO2 = { VO2,IO2 };
*/
			CSGTree fullOp = { leftOperand, rightOperand, opElement };
			
			CSG_Stack.push_back(fullOp);
		}
		else {
			int index = std::stoi(opElement, nullptr, 10);
			int countVertices = countVert[index];
			int countIndices = countInd[index];
			int shiftVertices = shiftVert[index];
			int shiftIndices = shiftInd[index];

			//fill matrices
			MatrixXd VertexOperand = GetSubMatrix(VertexPoints, shiftVertices, countVertices, 3);
			MatrixXi IndexOperand = GetSubMatrix(TriangleIndices, shiftIndices/3, countIndices/3, 3);
//			string testa = "{ VertexOperand, IndexOperand }";
			
			const CSGTree operand = { VertexOperand, IndexOperand };
			CSG_Stack.push_back(operand);
			
		}
	}
	
	CSGTree Final;
	
	//dW: this is the main function to compute the weight matrix
	int * ComputeCSG(char * operationString, double *allVerticesIn, int *allIndicesIn, int *verticesCount, int *indicesCount, int * verticesShift, int * indicesShift, int *binSizes) {//, int *PEext) {
		
			
		
			//dW: assign names for readability
			int vertexCount = binSizes[0];
			int triangleCount = binSizes[1];

			countVert = verticesCount;
			countInd = indicesCount;
			shiftVert = verticesShift;
			shiftInd = indicesShift;

			//dW: set matrices to values from vvvv
			VertexPoints = FillMatrix(allVerticesIn, vertexCount, 3);
			TriangleIndices = FillMatrix(allIndicesIn, triangleCount, 3);

			CSG_Stack.clear();
			std::vector<CSGTree>(CSG_Stack).swap(CSG_Stack);

//			const string oString = "0 0 1 union union 0 1 intersect minus";
			
	//		vector<string> opStringElements = split(oString);

			std::vector<std::string> opStringElements;

			split(opStringElements, operationString, is_any_of(" "));


//			solve csg tree
			for_each(opStringElements.begin(), opStringElements.end(), workThestack);

			Final = CSG_Stack[0];
/*
			MatrixXd VertexOperandA = GetSubMatrix(VertexPoints, verticesShift[0], verticesCount[0], 3);
			MatrixXi IndexOperandA = GetSubMatrix(TriangleIndices, indicesShift[0]/3, indicesCount[0]/3, 3);

			CSGTree leftOperand = { VertexOperandA ,IndexOperandA };

			MatrixXd VertexOperandB = GetSubMatrix(VertexPoints, verticesShift[1], verticesCount[1], 3);
			MatrixXi IndexOperandB = GetSubMatrix(TriangleIndices, indicesShift[1]/3, indicesCount[1]/3, 3);

			CSGTree rightOperand = { VertexOperandB ,IndexOperandB };

			igl::MeshBooleanType boolean_type = string_to_mesh_boolean_type("intersect");

			Final = { { VertexOperandA ,IndexOperandA },{ VertexOperandB ,IndexOperandB }, boolean_type };
*/
			int* resultArray = new int[3];
			resultArray[0] = Final.V().size() / 3;
			resultArray[1] = Final.F().size();
			resultArray[2] = Final.J().size();

			return resultArray;
			delete[] resultArray;

	}

	void getValues(double * verticesOut, int * indicesOut, int * refOut) {

		//		verticesOut = new double[VC.size()];
		MatrixXd VC = Final.cast_V<MatrixXd>();
		MatrixXi FC = Final.F();
		Eigen::Matrix<Eigen::DenseIndex,-1,1> JC = Final.J();
		for (int i = 0; i<JC.size(); i++)
		{
			
				refOut[i] = JC(i);
			
		}

		Map<Matrix<double, -1, -1, RowMajor>>(verticesOut, VC.rows(), VC.cols()) = VC;

		//dW: set array pointer with values from matrix
		Map<Matrix<int, -1, -1, RowMajor>>(indicesOut, FC.rows(), FC.cols()) = FC;

//		Map<Matrix<int, -1, -1, RowMajor>>(refOut, JC.rows(), JC.cols()) = JC;


	}
}