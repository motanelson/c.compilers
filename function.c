int factorial(int n) {
    if (n <= 1) {
        return 1;
    }
    return n * factorial(n - 1);
}

int main() {
    int num = 6;
    int result = factorial(num);
    printf("Factorial of %d is: %d\n", num, result);
    return 0;
}