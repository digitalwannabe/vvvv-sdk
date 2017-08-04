#pragma once

#include <stdexcept>
using namespace std;

namespace ARAPParam
{
	extern "C" { __declspec(dllexport) void doARAPParam(
		double *VIn, 
		int *IIn,  
		int *binSizes,
		double * Vuv); }
	
	extern "C" { __declspec(dllexport) void doLSCMParam(
		double *VIn,
		int *IIn,
		int *binSizes,
		double * Vuv); }//, int *PEext);}
//	extern "C" { __declspec(dllexport) void getValues(double * verticesOut, int * indicesOut, int * refOut); }
	extern "C" __declspec(dllexport) int ReleaseMemory(double* pArray)
	{
		delete[] pArray;
		//delete[] Usize;
		return 0;
	}
}

