// Soduko model

range I = 1..9; // row
range J = 1..9; // column
range K = 1..9; // number
range L = 1..3; // major square row
range M = 1..3; // major square column

dvar bool z[I,J,K]; // 1 if k in in position (i,j)

int Given[I,J,K] = ...; // the given numbers

maximize sum(i in I) sum(j in J) sum(k in K) Given[i,j,k]*z[i,j,k];

subject to
{
    forall(i in I, k in K)
       sum(j in J) z[i,j,k] == 1;

    forall(j in J, k in K)
       sum(i in I) z[i,j,k] == 1;

    forall(l in L, m in M, k in K)
        sum(i in I: i > (l-1)*3 && i <= l*3) sum(j in J: j > (m-1)*3 && j <= m*3) z[i,j,k] == 1;

 
}