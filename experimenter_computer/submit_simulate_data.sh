#!/bin/bash
#SBATCH --output=log/%j-generate_data.log
#SBATCH --job-name rt_synthetic_data
#SBATCH -p psych_day 
#SBATCH -t 23:00:00
#SBATCH --mem=80G
#SBATCH -n 1

# Set up the environment
module load miniconda
conda activate env_tda


# Run the python script
python3 -u simulate_realtime_data.py -n $1 -s $2 -e $3 -r $4
