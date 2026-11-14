int main() {
    int score = 85;
    printf("Testing conditions for score: %d\n", score);

    if (score >= 90) {
        printf("Grade: A\n");
    } else if (score >= 80) {
        printf("Grade: B\n");
    } else {
        printf("Grade: C or below\n");
    }

    int count = 3;
    while (count > 0) {
        printf("Countdown: %d\n", count);
        count = count - 1;
    }
    printf("Blastoff!\n");
    return 0;
}