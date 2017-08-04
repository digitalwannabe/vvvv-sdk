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



#include <Eigen/Geometry>
#include <Eigen/StdVector>
#include <algorithm>
#include <iostream>
#include <Eigen/Core>

#include <igl/readOFF.h>


#include <igl/arap.h>
#include <igl/boundary_loop.h>
#include <igl/harmonic.h>
#include <igl/map_vertices_to_circle.h>
#include <igl/lscm.h>

#include <string>
#include <vector>



#include "main.h"

namespace ARAPParam {


	using namespace Eigen;
	using namespace std;

	Eigen::MatrixXd V;
	Eigen::MatrixXi F;
	Eigen::MatrixXd V_uv;
	Eigen::MatrixXd initial_guess;

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

	
	void doARAPParam(double *VIn, int *IIn, int *binSizes, double * Vuv) {//, int *PEext) {
						
			//dW: assign names for readability
			int vertexCount = binSizes[0];
			int triangleCount = binSizes[1];

			//dW: set matrices to values from vvvv
			V = FillMatrix(VIn, vertexCount, 3);
			F = FillMatrix(IIn, triangleCount, 3);
			
			// Compute the initial solution for ARAP (harmonic parametrization)
			Eigen::VectorXi bnd;
			igl::boundary_loop(F, bnd);
			Eigen::MatrixXd bnd_uv;
			igl::map_vertices_to_circle(V, bnd, bnd_uv);

			igl::harmonic(V, F, bnd, bnd_uv, 1, initial_guess);
			
			// Add dynamic regularization to avoid to specify boundary conditions
			igl::ARAPData arap_data;
			arap_data.with_dynamics = true;
			Eigen::VectorXi b = Eigen::VectorXi::Zero(0);
			Eigen::MatrixXd bc = Eigen::MatrixXd::Zero(0, 0);

			// Initialize ARAP
			arap_data.max_iter = 100;
			// 2 means that we're going to *solve* in 2d
			arap_precomputation(V, F, 2, b, arap_data);

			// Solve arap using the harmonic map as initial guess
			V_uv = initial_guess;

			arap_solve(bc, arap_data, V_uv);

			Map<Matrix<double, -1, -1, RowMajor>>(Vuv, V_uv.rows(), V_uv.cols()) = V_uv;


	}

	void doLSCMParam(double *VIn, int *IIn, int *binSizes, double * Vuv) {//, int *PEext) {

																		  //dW: assign names for readability
		int vertexCount = binSizes[0];
		int triangleCount = binSizes[1];

		//dW: set matrices to values from vvvv
		V = FillMatrix(VIn, vertexCount, 3);
		F = FillMatrix(IIn, triangleCount, 3);

		// Fix two points on the boundary
		VectorXi bnd, b(2, 1);
		igl::boundary_loop(F, bnd);
		b(0) = bnd(0);
		b(1) = bnd(round(bnd.size() / 2));
		MatrixXd bc(2, 2);
		bc << 0, 0, 1, 0;

		// LSCM parametrization
		igl::lscm(V, F, b, bc, V_uv);

		Map<Matrix<double, -1, -1, RowMajor>>(Vuv, V_uv.rows(), V_uv.cols()) = V_uv;


	}



}